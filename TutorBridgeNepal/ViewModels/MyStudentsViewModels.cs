namespace TutorBridgeNepal.ViewModels;

public class MyStudentsPageViewModel
{
    public string Tab { get; set; } = "active";
    public string? Search { get; set; }
    public string? Grade { get; set; }
    public string? Subject { get; set; }
    public string Sort { get; set; } = "recent";

    public int ActiveCount { get; set; }
    public int PastCount { get; set; }
    public int AllCount { get; set; }
    public int TotalSessions { get; set; }
    public double AvgSessionsPerStudent { get; set; }
    public decimal AvgRatingGivenToTutor { get; set; }
    public int RatingCount { get; set; }

    public List<string> GradeOptions { get; set; } = new();
    public List<string> SubjectOptions { get; set; } = new();

    public List<TutorStudentCardViewModel> Students { get; set; } = new();
}

public class TutorStudentCardViewModel
{
    public int StudentProfileId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public string? GradeLevel { get; set; }
    public string? District { get; set; }
    public List<string> Subjects { get; set; } = new();
    public int SessionsCompleted { get; set; }
    public DateTime? LastSessionAt { get; set; }
    public DateTime? NextSessionAt { get; set; }
    public int? RatingGiven { get; set; }
    public bool IsNew { get; set; }
    public bool HasPendingRequest { get; set; }
    public int? PendingBookingId { get; set; }
    public DateTime? PendingRequestedFor { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
}

public class TutorStudentSessionRowViewModel
{
    public string Subject { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Status { get; set; } = string.Empty;
}