namespace TutorBridgeNepal.Models;

public class Booking
{
    public int Id { get; set; }
    public int StudentProfileId { get; set; }
    public StudentProfile StudentProfile { get; set; } = default!;
    public int TutorProfileId { get; set; }
    public TutorProfile TutorProfile { get; set; } = default!;
    public int TutorAvailabilitySlotId { get; set; }
    public TutorAvailabilitySlot TutorAvailabilitySlot { get; set; } = default!;
    public string Subject { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}