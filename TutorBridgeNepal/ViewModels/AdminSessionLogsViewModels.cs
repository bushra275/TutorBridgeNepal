namespace TutorBridgeNepal.ViewModels;

public class AdminSessionLogsViewModel
{
    public string AdminName { get; set; } = string.Empty;
    public string AdminInitials { get; set; } = string.Empty;

    // ---- KPI strip ----
    public int TotalSessions { get; set; }
    public int? TotalSessionsTrendPercent { get; set; }
    public int CompletedCount { get; set; }
    public int CompletedPercent { get; set; }
    public int OngoingNowCount { get; set; }
    public int CancelledCount { get; set; }
    public int CancelledPercent { get; set; }

    // ---- Tabs ----
    public string ActiveTab { get; set; } = "all";
    public int AllTabCount { get; set; }
    public int CompletedTabCount { get; set; }
    public int OngoingTabCount { get; set; }
    public int CancelledTabCount { get; set; }
    public int DisputedTabCount { get; set; }

    // ---- Filters ----
    public string? Search { get; set; }
    public string? SubjectFilter { get; set; }
    public string? StatusFilter { get; set; }
    public string DateRangeFilter { get; set; } = "all";
    public string? DistrictFilter { get; set; }
    public string Sort { get; set; } = "newest";
    public List<string> Subjects { get; set; } = new();
    public List<string> Districts { get; set; } = new();

    // ---- Results / paging ----
    public List<AdminSessionLogRowViewModel> Rows { get; set; } = new();
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 8;
    public int TotalMatching { get; set; }
    public int TotalPages => TotalMatching == 0 ? 1 : (int)Math.Ceiling(TotalMatching / (double)PageSize);
    public List<int?> PageWindow { get; set; } = new();

    // ---- Chart: sessions over the last 14 days ----
    public List<string> ChartLabels { get; set; } = new();
    public List<int> ChartValues { get; set; } = new();
    public int ChartMax => ChartValues.Any() ? Math.Max(ChartValues.Max(), 1) : 1;
}

public class AdminSessionLogRowViewModel
{
    public int BookingId { get; set; }
    public string SessionCode => $"#SES-{BookingId:D4}";

    public string StudentName { get; set; } = string.Empty;
    public string StudentInitials { get; set; } = string.Empty;
    public string TutorName { get; set; } = string.Empty;
    public string TutorInitials { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;

    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string DurationLabel { get; set; } = string.Empty;

    // "Live", "Completed", "Cancelled", "Disputed", "Missed", "Upcoming", "Pending"
    public string DisplayStatus { get; set; } = string.Empty;
    public string Mode { get; set; } = "Online";
    public bool IsDisputed { get; set; }
}