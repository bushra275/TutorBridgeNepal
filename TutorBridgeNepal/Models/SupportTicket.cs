namespace TutorBridgeNepal.Models;

public class SupportTicket
{
    public int Id { get; set; }

    // Exactly one of StudentProfileId / TutorProfileId is set, depending on
    // who submitted the ticket.
    public int? StudentProfileId { get; set; }
    public StudentProfile? StudentProfile { get; set; }

    public int? TutorProfileId { get; set; }
    public TutorProfile? TutorProfile { get; set; }

    public string Category { get; set; } = "Other"; // "Booking", "Messaging", "Account", "Other"
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    // "Open", "Resolved" - no admin UI to change this yet, defaults to Open
    public string Status { get; set; } = "Open";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}