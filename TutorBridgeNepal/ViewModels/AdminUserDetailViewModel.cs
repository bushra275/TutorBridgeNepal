namespace TutorBridgeNepal.ViewModels;

public class AdminUserDetailViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? District { get; set; }
    public string Role { get; set; } = string.Empty;
    public string IdCode { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsSuspended { get; set; }

    // Tutor-only
    public int? TutorProfileId { get; set; }
    public string? Subjects { get; set; }
    public int? YearsOfExperience { get; set; }
    public decimal? AverageRating { get; set; }
    public int? ReviewCount { get; set; }
    public string? Bio { get; set; }
    public bool? IsVerified { get; set; }
    public bool? VerificationRejected { get; set; }
    public DateTime? VerificationDecidedAt { get; set; }
    public List<string> CredentialTitles { get; set; } = new();

    // Student-only
    public int? StudentProfileId { get; set; }
    public string? GradeLevel { get; set; }
    public string? SchoolName { get; set; }
    public string? CurriculumBoard { get; set; }

    public int TotalSessions { get; set; }
    public int CompletedSessions { get; set; }
    public double HoursLearned { get; set; }
    public List<AdminUserSessionRow> RecentSessions { get; set; } = new();
}

public class AdminUserSessionRow
{
    public string OtherPartyName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Status { get; set; } = string.Empty;
}