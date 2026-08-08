namespace TutorBridgeNepal.ViewModels;

public class AdminUserManagementViewModel
{
    public string AdminName { get; set; } = string.Empty;
    public string AdminInitials { get; set; } = string.Empty;

    // KPI summary and tab counts - always reflect the whole platform, not
    // the current filter/search/page, same convention as the main Dashboard.
    public int TotalUsers { get; set; }
    public int TotalStudents { get; set; }
    public int TotalTutors { get; set; }
    public int PendingApprovalCount { get; set; }
    public int SuspendedCount { get; set; }
    public int? TotalUsersTrendPercent { get; set; }
    public int? StudentsTrendPercent { get; set; }
    public int? TutorsTrendPercent { get; set; }

    public string ActiveTab { get; set; } = "all";

    // Filters - echoed back into the form so selections persist across postbacks.
    public string? Search { get; set; }
    public string? RoleFilter { get; set; }
    public string? DistrictFilter { get; set; }
    public string? StatusFilter { get; set; }
    public string RegisteredFilter { get; set; } = "all";
    public string Sort { get; set; } = "name_asc";
    public List<string> Districts { get; set; } = new();

    public List<AdminUserRowFullViewModel> Rows { get; set; } = new();

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 8;
    public int TotalMatching { get; set; }
    public int TotalPages => TotalMatching == 0 ? 1 : (int)Math.Ceiling(TotalMatching / (double)PageSize);

    // Pagination widget page numbers to render; null entries render as "…".
    public List<int?> PageWindow { get; set; } = new();
}

public class AdminUserRowFullViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public string AvatarClass { get; set; } = string.Empty; // "", "purple", "yellow"
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty; // "Student" or "Tutor"
    public string IdCode { get; set; } = string.Empty; // STU-0241 / TUT-0512
    public string? SubLabel { get; set; } // "Grade 10" / "Chemistry, Physics"
    public string? District { get; set; }
    public DateTime JoinedAt { get; set; }
    public int SessionCount { get; set; }
    public string Status { get; set; } = string.Empty; // Active / Pending / Suspended / Rejected
    public int? TutorProfileId { get; set; } // needed for Approve/Reject
}