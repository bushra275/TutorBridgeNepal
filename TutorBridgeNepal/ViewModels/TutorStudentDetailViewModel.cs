namespace TutorBridgeNepal.ViewModels;

public class TutorStudentDetailViewModel
{
    public int StudentProfileId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public string? GradeLevel { get; set; }
    public string? SchoolName { get; set; }
    public string? District { get; set; }
    public string? CurriculumBoard { get; set; }
    public string? LearningGoal { get; set; }
    public int TotalSessions { get; set; }
    public int CompletedSessions { get; set; }

    public List<StudentSessionHistoryRow> RecentSessions { get; set; } = new();
}

public class StudentSessionHistoryRow
{
    public string Subject { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Status { get; set; } = string.Empty;
}