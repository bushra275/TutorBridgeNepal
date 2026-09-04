namespace TutorBridgeNepal.ViewModels;

public class SessionRequestsPageViewModel
{
    public string Tab { get; set; } = "pending";
    public string Sort { get; set; } = "newest";

    public int PendingCount { get; set; }
    public int AcceptedThisMonthCount { get; set; }
    public int DeclinedThisMonthCount { get; set; }
    public double? AvgResponseTimeMinutes { get; set; }
    public int? ResponseRatePercent { get; set; }

    public List<SessionRequestRowViewModel> Requests { get; set; } = new();
    public List<RecentlyAcceptedRowViewModel> RecentlyAccepted { get; set; } = new();
}

public class SessionRequestRowViewModel
{
    public int BookingId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentInitials { get; set; } = string.Empty;
    public string? GradeLevel { get; set; }
    public string? District { get; set; }
    public bool IsReturningStudent { get; set; }
    public int PriorSessionsCount { get; set; }
    public DateTime? LastSessionAt { get; set; }
    public int? LastRatingGiven { get; set; }
    public string Subject { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool DeclinedByTutor { get; set; }
    public DateTime RequestedAt { get; set; }
    public string? MeetingLink { get; set; }
}

public class RecentlyAcceptedRowViewModel
{
    public string StudentName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public DateTime SessionDate { get; set; }
    public DateTime AcceptedOn { get; set; }
    public double ResponseTimeMinutes { get; set; }
}