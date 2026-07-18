namespace TutorBridgeNepal.Models;

// Records the moment a student actually earned a badge. Rows are only ever
// inserted once the real underlying condition (session count, streak, etc.)
// has genuinely been met - see StudentController.Progress() for the checks.
public class StudentAchievement
{
    public int Id { get; set; }
    public int StudentProfileId { get; set; }
    public StudentProfile StudentProfile { get; set; } = default!;

    // Matches one of the keys defined in StudentController's achievement list,
    // e.g. "sessions_10", "streak_7", "top_student_month", "sessions_25"
    public string AchievementKey { get; set; } = string.Empty;
    public DateTime UnlockedAt { get; set; } = DateTime.Now;
}