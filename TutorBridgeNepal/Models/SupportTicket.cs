namespace TutorBridgeNepal.Models;

public class SupportTicket
{
    public int Id { get; set; }
    public int StudentProfileId { get; set; }
    public StudentProfile StudentProfile { get; set; } = default!;

    public string Category { get; set; } = "Other"; // "Booking", "Messaging", "Account", "Other"
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    // "Open", "Resolved" - no admin UI to change this yet, defaults to Open
    public string Status { get; set; } = "Open";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}