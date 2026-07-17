namespace TutorBridgeNepal.ViewModels;

public class StudentDashboardViewModel
{
    public string FullName { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public string? GradeLevel { get; set; }
    public string? District { get; set; }

    public int TotalSessions { get; set; }
    public int CompletedSessions { get; set; }
    public double HoursLearned { get; set; }
    public int ActiveTutorsCount { get; set; }
    public int UpcomingCount { get; set; }

    public List<BookingRowViewModel> UpcomingSessions { get; set; } = new();
    public List<BookingRowViewModel> RecentSessions { get; set; } = new();
    public List<TutorRowViewModel> MyTutors { get; set; } = new();
    public List<SubjectProgressViewModel> SubjectProgress { get; set; } = new();
    public List<RecentMessageViewModel> RecentMessages { get; set; } = new();
}

public class RecentMessageViewModel
{
    public int TutorProfileId { get; set; }
    public string TutorName { get; set; } = string.Empty;
    public string TutorInitials { get; set; } = string.Empty;
    public string Preview { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public bool IsFromStudent { get; set; }
}

public class BookingRowViewModel
{
    public int BookingId { get; set; }
    public string TutorName { get; set; } = string.Empty;
    public string TutorInitials { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class TutorRowViewModel
{
    public int TutorProfileId { get; set; }
    public string TutorName { get; set; } = string.Empty;
    public string TutorInitials { get; set; } = string.Empty;
    public string Subjects { get; set; } = string.Empty;
    public decimal AverageRating { get; set; }
}

public class SubjectProgressViewModel
{
    public string Subject { get; set; } = string.Empty;
    public int CompletedSessions { get; set; }
}