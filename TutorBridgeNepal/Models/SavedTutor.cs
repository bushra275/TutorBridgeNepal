namespace TutorBridgeNepal.Models;

public class SavedTutor
{
    public int Id { get; set; }

    public int StudentProfileId { get; set; }
    public StudentProfile StudentProfile { get; set; } = default!;

    public int TutorProfileId { get; set; }
    public TutorProfile TutorProfile { get; set; } = default!;

    public DateTime SavedAt { get; set; } = DateTime.Now;
}