namespace TutorBridgeNepal.ViewModels;

public class TutorProfilePageViewModel
{
    public string FullName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string Initials { get; set; } = string.Empty;
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

    public List<TutorSubjectRateRowViewModel> SubjectRates { get; set; } = new();
    public List<TutorCredentialRowViewModel> Credentials { get; set; } = new();

    public int ProfileCompletionPercent { get; set; }
    public string? ProfileCompletionHint { get; set; }
}

public class TutorSubjectRateRowViewModel
{
    public int Id { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal RatePerHour { get; set; }
}

public class TutorCredentialRowViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public string Icon { get; set; } = "📄";
}