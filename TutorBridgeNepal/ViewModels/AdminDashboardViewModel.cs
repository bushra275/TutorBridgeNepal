namespace TutorBridgeNepal.ViewModels;

public class AdminDashboardViewModel
{
    public string AdminName { get; set; } = string.Empty;
    public string AdminInitials { get; set; } = string.Empty;
    public string TodayLabel { get; set; } = string.Empty;

    public int TotalUsers { get; set; }
    public int ActiveTutors { get; set; }
    public int SessionsThisMonth { get; set; }
    public int PendingVerificationsCount { get; set; }
    public int OpenComplaintsCount { get; set; }

    // Real month-over-month growth, null when there's no prior-month baseline
    // to compare against (e.g. brand new platform).
    public int? TotalUsersTrendPercent { get; set; }
    public int? ActiveTutorsTrendPercent { get; set; }
    public int? SessionsTrendPercent { get; set; }

    public string ChartMonthLabel { get; set; } = string.Empty;

    // Days of the current month so far, oldest first.
    public List<string> ChartLabels { get; set; } = new();
    public List<int> ChartValues { get; set; } = new();
    public int ChartMax => ChartValues.Any() ? Math.Max(ChartValues.Max(), 1) : 1;

    public List<AdminSessionRowViewModel> RecentSessions { get; set; } = new();
    public List<AdminComplaintRowViewModel> OpenComplaints { get; set; } = new();
    public List<AdminVerificationRowViewModel> PendingVerifications { get; set; } = new();
    public List<AdminUserRowViewModel> RecentRegistrations { get; set; } = new();
    public List<AdminActivityItemViewModel> RecentActivity { get; set; } = new();

    // All computed from real data - no fabricated trend percentages, since
    // there's no historical snapshot table to compare against.
    public int TutorApprovalRatePercent { get; set; }
    public int SessionCompletionRatePercent { get; set; }
    public int StudentSatisfactionPercent { get; set; }
    public int ComplaintResolutionRatePercent { get; set; }
}

public class AdminSessionRowViewModel
{
    public string StudentName { get; set; } = string.Empty;
    public string TutorName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class AdminComplaintRowViewModel
{
    public int SupportTicketId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string ReporterName { get; set; } = string.Empty;
    public string ReporterRole { get; set; } = string.Empty; // "Student" or "Tutor"
    public DateTime CreatedAt { get; set; }
}

public class AdminVerificationRowViewModel
{
    public int TutorProfileId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public string? FirstSubject { get; set; }
    public string? District { get; set; }
}

public class AdminUserRowViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty; // "Student" or "Tutor"
    public string? District { get; set; }
    public string StatusLabel { get; set; } = string.Empty; // "Active" or "Pending"
}

public class AdminActivityItemViewModel
{
    public DateTime Timestamp { get; set; }
    public string BoldLead { get; set; } = string.Empty;
    public string RestText { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public string DotClass { get; set; } = string.Empty; // "green", "orange", "red"
    public string TagClass { get; set; } = string.Empty; // "student", "session", "verify", "review"
}