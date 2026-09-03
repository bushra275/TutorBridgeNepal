namespace TutorBridgeNepal.ViewModels;

public class TutorProfilePageViewModel
{
    public string FullName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string Initials { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public bool IsVerified { get; set; }
    public bool IsTopTutor { get; set; }
    public int TopTutorYear { get; set; }

    public List<string> SubjectTags { get; set; } = new();
    public string? District { get; set; }
    public string? Province { get; set; }

    public int YearsOfExperience { get; set; }
    public int SessionsCompleted { get; set; }
    public decimal AverageRating { get; set; }
    public int ReviewCount { get; set; }

    public string? Bio { get; set; }
    public string? TeachingMode { get; set; }
    public List<string> Languages { get; set; } = new();
    public List<string> TeachingStyleTags { get; set; } = new();

    public List<TutorSubjectRowViewModel> Subjects { get; set; } = new();
    public List<TutorCredentialRowViewModel> Credentials { get; set; } = new();

    // The 4-slot verification checklist (Citizenship, CV/Resume, Degree
    // Certificate, Police Report) shown on the tutor's own Profile page,
    // mirroring what Admin > Tutor Verification checks for. Separate from
    // Credentials above (which can also list free-text entries like a
    // listed degree title with no uploaded file).
    public List<TutorDocumentSlotViewModel> RequiredDocuments { get; set; } = new();

    public int ProfileCompletionPercent { get; set; }
    public string? ProfileCompletionHint { get; set; }
}

public class TutorDocumentSlotViewModel
{
    public string DocumentType { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Icon { get; set; } = "📄";
    public bool IsUploaded { get; set; }
    public int? CredentialId { get; set; }
    public string? OriginalFileName { get; set; }
    public DateTime? UploadedAt { get; set; }
}

public class TutorVerificationPendingViewModel
{
    public string FullName { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public bool IsRejected { get; set; }
    public DateTime SubmittedAt { get; set; }
    public List<TutorDocumentSlotViewModel> RequiredDocuments { get; set; } = new();
    public bool AllDocumentsUploaded => RequiredDocuments.All(d => d.IsUploaded);

    // Set by an admin's "Request more info" action - shown as a banner
    // above the checklist so the tutor knows exactly what to fix.
    public string? AdminNote { get; set; }

    public DateTime? InterviewScheduledAt { get; set; }
    public string? InterviewMeetingLink { get; set; }
    public bool InterviewIsUpcoming => InterviewScheduledAt.HasValue && InterviewScheduledAt.Value > DateTime.Now;
}

public class TutorSubjectRowViewModel
{
    public int Id { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }
}
public class TutorCredentialRowViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public string Icon { get; set; } = "📄";
}