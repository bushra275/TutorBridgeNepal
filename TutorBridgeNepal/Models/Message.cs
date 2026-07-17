namespace TutorBridgeNepal.Models;

public class Message
{
    public int Id { get; set; }

    public int StudentProfileId { get; set; }
    public StudentProfile StudentProfile { get; set; } = default!;

    public int TutorProfileId { get; set; }
    public TutorProfile TutorProfile { get; set; } = default!;

    // "Student" or "Tutor" - who sent this message
    public string SenderRole { get; set; } = "Student";

    public string Content { get; set; } = string.Empty;
    public DateTime SentAt { get; set; } = DateTime.Now;
    public bool IsRead { get; set; }
}