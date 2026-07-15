namespace TutorBridgeNepal.ViewModels;

public class StudentSessionsViewModel
{
    public string ActiveTab { get; set; } = "upcoming";
    public string? Subject { get; set; }
    public int? TutorProfileId { get; set; }
    public string Sort { get; set; } = "newest";

    public List<SessionRowViewModel> Sessions { get; set; } = new();
    public List<string> SubjectOptions { get; set; } = new();
    public List<TutorRowViewModel> TutorOptions { get; set; } = new();

    public int UpcomingCount { get; set; }
    public int CompletedCount { get; set; }
    public int CancelledCount { get; set; }
    public int AllCount { get; set; }

    public int TotalSessions { get; set; }
    public double HoursLearned { get; set; }
    public int CompletionRatePercent { get; set; }
    public int DecidedSessionsCount { get; set; }
}

public class SessionRowViewModel
{
    public int BookingId { get; set; }
    public int TutorProfileId { get; set; }
    public string TutorName { get; set; } = string.Empty;
    public string TutorInitials { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Status { get; set; } = string.Empty;
}