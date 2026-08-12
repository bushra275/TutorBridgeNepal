namespace TutorBridgeNepal.ViewModels;

public class AdminReportsViewModel
{
    public string AdminName { get; set; } = string.Empty;
    public string AdminInitials { get; set; } = string.Empty;

    public string QuarterFilter { get; set; } = "current";
    public string QuarterLabel { get; set; } = string.Empty;

    // ---- Overview KPIs ----
    public int TotalPlatformUsers { get; set; }
    public int? TotalUsersTrendPercent { get; set; }
    public int SessionsThisQuarter { get; set; }
    public int? SessionsTrendPercent { get; set; }
    public decimal AvgSessionRating { get; set; }
    public string RatingTrendLabel { get; set; } = "Stable";
    public int RetentionRatePercent { get; set; }
    public int? RetentionTrendPercent { get; set; }

    // ---- User growth chart (last 6 months, shown on Overview) ----
    public List<string> GrowthMonthLabels { get; set; } = new();
    public List<int> GrowthStudentCounts { get; set; } = new();
    public List<int> GrowthTutorCounts { get; set; } = new();
    public int GrowthChartMax => Math.Max(1, new[] { GrowthStudentCounts.DefaultIfEmpty(0).Max(), GrowthTutorCounts.DefaultIfEmpty(0).Max() }.Max());

    // ---- Sessions by subject (donut, shown on Overview) ----
    public List<SubjectShareViewModel> SubjectShares { get; set; } = new();
    public string? FastestGrowingSubject { get; set; }
    public int? FastestGrowingSubjectPercent { get; set; }
    public int TotalSubjectSessions { get; set; }

    // ---- Top performing tutors (top 4, shown on Overview) ----
    public List<TutorPerformanceRowViewModel> TopTutors { get; set; } = new();

    // ---- Platform health summary (shown on Overview) ----
    public int TutorApprovalRatePercent { get; set; }
    public int? TutorApprovalTrendPercent { get; set; }
    public int SessionCompletionRatePercent { get; set; }
    public int? SessionCompletionTrendPercent { get; set; }
    public int StudentSatisfactionPercent { get; set; }
    public string StudentSatisfactionTrendLabel { get; set; } = "Stable";
    public int ComplaintResolutionRatePercent { get; set; }
    public int? ComplaintResolutionTrendPercent { get; set; }

    // ---- User growth tab (extended, 12 months + by district) ----
    public List<string> Growth12MonthLabels { get; set; } = new();
    public List<int> Growth12MonthStudents { get; set; } = new();
    public List<int> Growth12MonthTutors { get; set; } = new();
    public int Growth12MonthMax => Math.Max(1, new[] { Growth12MonthStudents.DefaultIfEmpty(0).Max(), Growth12MonthTutors.DefaultIfEmpty(0).Max() }.Max());
    public int NewStudentsThisQuarter { get; set; }
    public int NewTutorsThisQuarter { get; set; }
    public List<DistrictGrowthRowViewModel> GrowthByDistrict { get; set; } = new();

    // ---- Tutor performance tab (full ranked list) ----
    public List<TutorPerformanceRowViewModel> AllTutorsPerformance { get; set; } = new();

    // ---- Subject demand tab ----
    public List<SubjectDemandRowViewModel> SubjectDemand { get; set; } = new();
}

public class SubjectShareViewModel
{
    public string Subject { get; set; } = string.Empty;
    public int Count { get; set; }
    public int Percent { get; set; }
    public string ColorClass { get; set; } = "rp-c1";
}

public class TutorPerformanceRowViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public string Subjects { get; set; } = string.Empty;
    public int Sessions { get; set; }
    public decimal Rating { get; set; }
    public int CompletionPercent { get; set; }
}

public class DistrictGrowthRowViewModel
{
    public string District { get; set; } = string.Empty;
    public int StudentCount { get; set; }
    public int TutorCount { get; set; }
}

public class SubjectDemandRowViewModel
{
    public string Subject { get; set; } = string.Empty;
    public int Sessions { get; set; }
    public int Percent { get; set; }
    public int? GrowthPercent { get; set; }
    public decimal AvgRating { get; set; }
}