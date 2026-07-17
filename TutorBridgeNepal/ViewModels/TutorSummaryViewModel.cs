namespace TutorBridgeNepal.ViewModels;

public class TutorSummaryViewModel
{
    public int TutorProfileId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public string Subjects { get; set; } = string.Empty;
    public string? District { get; set; }
    public int YearsOfExperience { get; set; }
    public decimal AverageRating { get; set; }
    public bool IsVerified { get; set; }
    public int CompletedSessionsCount { get; set; }

    public List<string> SubjectTags =>
        Subjects.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}

public class HomeIndexViewModel
{
    public List<TutorSummaryViewModel> TopTutors { get; set; } = new();
}

public class FindTutorsViewModel
{
    public string? Query { get; set; }
    public List<TutorSummaryViewModel> Results { get; set; } = new();
}