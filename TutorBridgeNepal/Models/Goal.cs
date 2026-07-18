namespace TutorBridgeNepal.Models;

public class Goal
{
    public int Id { get; set; }
    public int StudentProfileId { get; set; }
    public StudentProfile StudentProfile { get; set; } = default!;
    public string? Subject { get; set; }
    public string Description { get; set; } = string.Empty;

    // "NotStarted", "InProgress", "Completed"
    public string Status { get; set; } = "NotStarted";
    public DateTime? DueDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}