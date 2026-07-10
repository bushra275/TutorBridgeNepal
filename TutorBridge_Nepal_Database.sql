/* TutorBridge Nepal - MS SQL Server starter database */

CREATE DATABASE TutorBridgeNepalFYPDB;
GO

USE TutorBridgeNepalFYPDB;
GO

CREATE TABLE Roles (
    RoleId INT IDENTITY(1,1) PRIMARY KEY,
    RoleName NVARCHAR(30) NOT NULL UNIQUE
);

INSERT INTO Roles (RoleName)
VALUES ('Student'), ('Tutor'), ('Admin');

CREATE TABLE Users (
    UserId INT IDENTITY(1,1) PRIMARY KEY,
    RoleId INT NOT NULL,
    FullName NVARCHAR(120) NOT NULL,
    Email NVARCHAR(150) NOT NULL UNIQUE,
    PhoneNumber NVARCHAR(20) NULL,
    District NVARCHAR(80) NULL,
    PasswordHash NVARCHAR(500) NOT NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleId) REFERENCES Roles(RoleId)
);

CREATE TABLE StudentProfiles (
    StudentProfileId INT IDENTITY(1,1) PRIMARY KEY,
    UserId INT NOT NULL UNIQUE,
    GradeLevel NVARCHAR(80) NULL,
    LearningGoal NVARCHAR(300) NULL,
    CONSTRAINT FK_StudentProfiles_Users FOREIGN KEY (UserId) REFERENCES Users(UserId)
);

CREATE TABLE TutorProfiles (
    TutorProfileId INT IDENTITY(1,1) PRIMARY KEY,
    UserId INT NOT NULL UNIQUE,
    Bio NVARCHAR(1000) NULL,
    Subjects NVARCHAR(300) NOT NULL,
    YearsOfExperience INT NOT NULL DEFAULT 0,
    HourlyRate DECIMAL(10,2) NOT NULL DEFAULT 0,
    QualificationFilePath NVARCHAR(300) NULL,
    IsVerified BIT NOT NULL DEFAULT 0,
    VerifiedAt DATETIME2 NULL,
    VerifiedByAdminUserId INT NULL,
    AverageRating DECIMAL(3,2) NOT NULL DEFAULT 0,
    TotalReviews INT NOT NULL DEFAULT 0,
    CONSTRAINT FK_TutorProfiles_Users FOREIGN KEY (UserId) REFERENCES Users(UserId),
    CONSTRAINT FK_TutorProfiles_VerifiedBy FOREIGN KEY (VerifiedByAdminUserId) REFERENCES Users(UserId)
);

CREATE TABLE TutorAvailabilitySlots (
    SlotId INT IDENTITY(1,1) PRIMARY KEY,
    TutorProfileId INT NOT NULL,
    StartTime DATETIME2 NOT NULL,
    EndTime DATETIME2 NOT NULL,
    IsBooked BIT NOT NULL DEFAULT 0,
    CONSTRAINT FK_TutorAvailabilitySlots_TutorProfiles FOREIGN KEY (TutorProfileId) REFERENCES TutorProfiles(TutorProfileId),
    CONSTRAINT CK_TutorAvailabilitySlots_Time CHECK (EndTime > StartTime)
);

CREATE TABLE Bookings (
    BookingId INT IDENTITY(1,1) PRIMARY KEY,
    StudentProfileId INT NOT NULL,
    TutorProfileId INT NOT NULL,
    SlotId INT NOT NULL UNIQUE,
    Subject NVARCHAR(120) NOT NULL,
    Status NVARCHAR(30) NOT NULL DEFAULT 'Pending',
    Notes NVARCHAR(500) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Bookings_StudentProfiles FOREIGN KEY (StudentProfileId) REFERENCES StudentProfiles(StudentProfileId),
    CONSTRAINT FK_Bookings_TutorProfiles FOREIGN KEY (TutorProfileId) REFERENCES TutorProfiles(TutorProfileId),
    CONSTRAINT FK_Bookings_Slots FOREIGN KEY (SlotId) REFERENCES TutorAvailabilitySlots(SlotId),
    CONSTRAINT CK_Bookings_Status CHECK (Status IN ('Pending', 'Confirmed', 'Completed', 'Cancelled'))
);

CREATE TABLE Reviews (
    ReviewId INT IDENTITY(1,1) PRIMARY KEY,
    BookingId INT NOT NULL UNIQUE,
    StudentProfileId INT NOT NULL,
    TutorProfileId INT NOT NULL,
    Rating INT NOT NULL,
    Comment NVARCHAR(700) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Reviews_Bookings FOREIGN KEY (BookingId) REFERENCES Bookings(BookingId),
    CONSTRAINT FK_Reviews_StudentProfiles FOREIGN KEY (StudentProfileId) REFERENCES StudentProfiles(StudentProfileId),
    CONSTRAINT FK_Reviews_TutorProfiles FOREIGN KEY (TutorProfileId) REFERENCES TutorProfiles(TutorProfileId),
    CONSTRAINT CK_Reviews_Rating CHECK (Rating BETWEEN 1 AND 5)
);

CREATE TABLE Messages (
    MessageId INT IDENTITY(1,1) PRIMARY KEY,
    SenderUserId INT NOT NULL,
    ReceiverUserId INT NOT NULL,
    BookingId INT NULL,
    MessageText NVARCHAR(1000) NOT NULL,
    SentAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    IsRead BIT NOT NULL DEFAULT 0,
    CONSTRAINT FK_Messages_Sender FOREIGN KEY (SenderUserId) REFERENCES Users(UserId),
    CONSTRAINT FK_Messages_Receiver FOREIGN KEY (ReceiverUserId) REFERENCES Users(UserId),
    CONSTRAINT FK_Messages_Bookings FOREIGN KEY (BookingId) REFERENCES Bookings(BookingId)
);

CREATE TABLE Notifications (
    NotificationId INT IDENTITY(1,1) PRIMARY KEY,
    UserId INT NOT NULL,
    Title NVARCHAR(120) NOT NULL,
    Body NVARCHAR(500) NOT NULL,
    IsRead BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Notifications_Users FOREIGN KEY (UserId) REFERENCES Users(UserId)
);

CREATE INDEX IX_Users_Email ON Users(Email);
CREATE INDEX IX_TutorProfiles_Verified ON TutorProfiles(IsVerified);
CREATE INDEX IX_TutorAvailabilitySlots_TutorTime ON TutorAvailabilitySlots(TutorProfileId, StartTime, EndTime);
CREATE INDEX IX_Bookings_Student ON Bookings(StudentProfileId);
CREATE INDEX IX_Bookings_Tutor ON Bookings(TutorProfileId);
GO

/* Safe booking pattern:
   Use a transaction and lock the selected slot before creating booking.
*/
CREATE OR ALTER PROCEDURE CreateBookingSafely
    @StudentProfileId INT,
    @TutorProfileId INT,
    @SlotId INT,
    @Subject NVARCHAR(120),
    @Notes NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    IF EXISTS (
        SELECT 1
        FROM TutorAvailabilitySlots WITH (UPDLOCK, HOLDLOCK)
        WHERE SlotId = @SlotId
          AND TutorProfileId = @TutorProfileId
          AND IsBooked = 0
    )
    BEGIN
        UPDATE TutorAvailabilitySlots
        SET IsBooked = 1
        WHERE SlotId = @SlotId;

        INSERT INTO Bookings (StudentProfileId, TutorProfileId, SlotId, Subject, Notes, Status)
        VALUES (@StudentProfileId, @TutorProfileId, @SlotId, @Subject, @Notes, 'Confirmed');
    END
    ELSE
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50001, 'This tutor slot is already booked or unavailable.', 1;
    END

    COMMIT TRANSACTION;
END;
GO