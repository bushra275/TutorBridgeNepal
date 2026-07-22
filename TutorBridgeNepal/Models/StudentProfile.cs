namespace TutorBridgeNepal.Models;

public class StudentProfile
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = default!;
    public string? GradeLevel { get; set; }
    public string? LearningGoal { get; set; }

    // Academic details
    public string? SchoolName { get; set; }
    public string? CurriculumBoard { get; set; }
    public string? SubjectsEnrolled { get; set; } // comma-separated, same pattern as TutorProfile.Subjects

    // Notification preferences - persisted for real, but note there is no
    // email/push delivery pipeline built yet (same gap as password reset).
    public bool NotifySessionReminders { get; set; } = true;
    public bool NotifyNewMessages { get; set; } = true;
    public bool NotifyProgressUpdates { get; set; } = false;

    // Privacy
    public bool ShowProfileToTutors { get; set; } = true;
}