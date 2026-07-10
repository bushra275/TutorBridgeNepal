namespace TutorBridgeNepal.Models;

public class StudentProfile
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = default!;
    public string? GradeLevel { get; set; }
    public string? LearningGoal { get; set; }
}