namespace TutorBridgeNepal.ViewModels;

public class TutorPreviewProfileViewModel
{
    public string? PhotoUrl { get; set; }
    public string Initials { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public bool IsVerified { get; set; }
    public string? TeachingMode { get; set; }
    public string? District { get; set; }
    public int YearsOfExperience { get; set; }
    public decimal AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public string? Bio { get; set; }

    public List<TutorSubjectRateRowViewModel> SubjectRates { get; set; } = new();
    public List<PreviewReviewRowViewModel> RecentReviews { get; set; } = new();
}

public class PreviewReviewRowViewModel
{
    public string StudentInitials { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string? Comment { get; set; }
}