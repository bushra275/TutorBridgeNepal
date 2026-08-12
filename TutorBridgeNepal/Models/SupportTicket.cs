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

    // "Open", "UnderReview", "Resolved"
    public string Status { get; set; } = "Open";
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // "High", "Medium", "Low" - defaults to Medium at submission (the
    // filer isn't the one who decides how urgent it is); an admin can
    // re-set it from Admin > Complaints.
    public string Severity { get; set; } = "Medium";

    // Set by AdminController.ResolveComplaint when the ticket moves to
    // "Resolved" - what the admin actually did about it.
    public string? ResolutionNote { get; set; }
    public DateTime? ResolvedAt { get; set; }

    // Optional - set when a "Booking" category ticket is about a specific
    // session, so Admin > Session Logs can show it as Disputed and link the
    // two together. Null for tickets not tied to a particular session.
    public int? BookingId { get; set; }
    public Booking? Booking { get; set; }
}