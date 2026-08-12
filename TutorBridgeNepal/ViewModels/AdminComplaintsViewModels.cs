namespace TutorBridgeNepal.ViewModels;

public class AdminComplaintsViewModel
{
    public string AdminName { get; set; } = string.Empty;
    public string AdminInitials { get; set; } = string.Empty;

    // ---- KPI strip ----
    public int OpenCount { get; set; }
    public bool HasUrgentOpen { get; set; }
    public int UnderReviewCount { get; set; }
    public string MonthLabel { get; set; } = string.Empty;
    public int ResolvedThisMonth { get; set; }
    public int? ResolvedTrendPercent { get; set; }
    public double? AvgResolutionDays { get; set; }
    public int ResolutionRatePercent { get; set; }

    // ---- Tabs ----
    public string ActiveTab { get; set; } = "open";
    public int OpenTabCount { get; set; }
    public int UnderReviewTabCount { get; set; }
    public int ResolvedTabCount { get; set; }
    public int AllTabCount { get; set; }

    // ---- Filters ----
    public string? Search { get; set; }
    public string? ComplaintType { get; set; }
    public string? SeverityFilter { get; set; }
    public string DateFiledFilter { get; set; } = "all";
    public string Sort { get; set; } = "newest";
    public List<string> ComplaintTypes { get; set; } = new();

    // ---- Results ----
    public List<AdminComplaintCardViewModel> Cards { get; set; } = new();
    public int VisibleCount { get; set; }
    public int TotalMatching { get; set; }
    public bool HasMore => TotalMatching > VisibleCount;
    public int RemainingCount => Math.Max(0, TotalMatching - VisibleCount);

    // ---- Secondary preview sections (only shown on the "open" tab) ----
    public List<AdminComplaintTableRowViewModel> UnderReviewPreview { get; set; } = new();
    public List<AdminComplaintTableRowViewModel> ResolvedPreview { get; set; } = new();
}

public class AdminComplaintCardViewModel
{
    public int Id { get; set; }
    public string ComplaintCode => $"CMP-{Id:D4}";
    public string Title { get; set; } = string.Empty;
    public string Severity { get; set; } = "Medium";
    public string Status { get; set; } = "Open";

    public string FilerName { get; set; } = string.Empty;
    public string FilerInitials { get; set; } = string.Empty;
    public string FilerRole { get; set; } = string.Empty; // "Student" or "Tutor"
    public string? FilerEmail { get; set; }

    public string? AgainstName { get; set; }
    public string? AgainstInitials { get; set; }
    public string? AgainstRole { get; set; }
    public string? AgainstEmail { get; set; }

    public string? SessionCode { get; set; }
    public string? SessionSubject { get; set; }
    public DateTime? SessionDate { get; set; }

    public string Message { get; set; } = string.Empty;
    public DateTime FiledAt { get; set; }
    public string? ResolutionNote { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

public class AdminComplaintTableRowViewModel
{
    public int Id { get; set; }
    public string ComplaintCode => $"CMP-{Id:D4}";
    public string Title { get; set; } = string.Empty;
    public string FilerName { get; set; } = string.Empty;
    public string? AgainstName { get; set; }
    public DateTime FiledAt { get; set; }
    public string Severity { get; set; } = "Medium";
    public string Status { get; set; } = "Open";
    public string? ResolutionNote { get; set; }
    public DateTime? ResolvedAt { get; set; }
}