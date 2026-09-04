namespace TutorBridgeNepal.ViewModels;

public class AdminTutorVerificationViewModel
{
    public int TutorProfileId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AdminName { get; set; } = string.Empty;
    public string AdminInitials { get; set; } = string.Empty;

    public string MonthLabel { get; set; } = string.Empty;
    public int PendingCount { get; set; }
    public int ApprovedThisMonth { get; set; }
    public int? ApprovedTrendPercent { get; set; }
    public int RejectedThisMonth { get; set; }
    public int? RejectedRatePercent { get; set; }
    public double? AvgReviewTimeDays { get; set; }
    public int ApprovalRatePercent { get; set; }

    public string ActiveTab { get; set; } = "pending";
    public int PendingTabCount { get; set; }
    public int ApprovedTabCount { get; set; }
    public int RejectedTabCount { get; set; }
    public int AllTabCount { get; set; }

    public string? Search { get; set; }
    public string? SubjectFilter { get; set; }
    public string? DistrictFilter { get; set; }
    public string SubmittedFilter { get; set; } = "all";
    public string Sort { get; set; } = "newest";
    public List<string> Subjects { get; set; } = new();
    public List<string> Districts { get; set; } = new();

    public List<AdminTutorVerificationRowViewModel> Applications { get; set; } = new();
    public int VisibleCount { get; set; }
    public int TotalMatching { get; set; }

    public bool HasMore => TotalMatching > Applications.Count;
    public int RemainingCount => Math.Max(0, TotalMatching - Applications.Count);
}

public class AdminTutorVerificationRowViewModel
{
    public int TutorProfileId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? MaskedPhone { get; set; }
    public string? District { get; set; }
    public List<string> Subjects { get; set; } = new();
    public string? Education { get; set; }
    public string? ExperienceSummary { get; set; }
    public int YearsOfExperience { get; set; }
    public DateTime SubmittedAt { get; set; }
    public int DaysAgo { get; set; }
    public string UrgencyClass { get; set; } = "orange"; // "orange" or "red"
    public List<AdminTutorVerificationDocumentViewModel> Documents { get; set; } = new();
    public string Status { get; set; } = "Pending";

    // Set once an admin has used "Request more info" - shown back on this
    // card so the admin can see what they last asked for, and cleared
    // automatically once the tutor re-uploads a document.
    public string? VerificationNote { get; set; }

    public bool AllDocumentsUploaded { get; set; }
    public DateTime? InterviewScheduledAt { get; set; }
    public string? InterviewMeetingLink { get; set; }
    public DateTime? InterviewCompletedAt { get; set; }

    // The scheduled time has arrived, but the admin hasn't confirmed the
    // interview actually took place yet.
    public bool InterviewTimeHasArrived => InterviewScheduledAt.HasValue && InterviewScheduledAt.Value <= DateTime.Now;

    // Approve/Reject gate on this, not just the clock - an admin must
    // explicitly confirm the interview happened.
    public bool InterviewHasHappened => InterviewCompletedAt.HasValue;
}

public class AdminTutorVerificationDocumentViewModel
{
    public int? CredentialId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public bool IsMissing { get; set; }
}