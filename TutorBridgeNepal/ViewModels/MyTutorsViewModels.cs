namespace TutorBridgeNepal.ViewModels;

public class MyTutorsPageViewModel
{
    public string ActiveTab { get; set; } = "active";
    public string? Subject { get; set; }
    public string Sort { get; set; } = "recent";

    public int ActiveTutorsCount { get; set; }
    public int SavedTutorsCount { get; set; }
    public int PastTutorsCount { get; set; }
    public int AllTutorsCount { get; set; }
    public int SessionsDoneCount { get; set; }
    public double HoursLearned { get; set; }

    public List<MyTutorCardViewModel> Tutors { get; set; } = new();
    public List<MyTutorCardViewModel> SavedPreview { get; set; } = new();
    public List<TutorSessionHistoryRow> RecentHistory { get; set; } = new();
    public List<string> SubjectOptions { get; set; } = new();
}

public class MyTutorCardViewModel
{
    public int TutorProfileId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public string Subjects { get; set; } = string.Empty;
    public string? District { get; set; }
    public int YearsOfExperience { get; set; }
    public decimal AverageRating { get; set; }
    public bool IsVerified { get; set; }
    public bool IsSaved { get; set; }
    public int SessionsWithStudent { get; set; }
    public DateTime? LastSessionAt { get; set; }
    public DateTime? NextSessionAt { get; set; }

    public List<string> SubjectTags =>
        Subjects.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}

public class TutorSessionHistoryRow
{
    public int TutorProfileId { get; set; }
    public string TutorName { get; set; } = string.Empty;
    public string TutorInitials { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Status { get; set; } = string.Empty;
}