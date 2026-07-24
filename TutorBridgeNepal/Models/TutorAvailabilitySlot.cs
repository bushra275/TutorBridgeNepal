namespace TutorBridgeNepal.Models;

public class TutorAvailabilitySlot
{
    public int Id { get; set; }
    public int TutorProfileId { get; set; }
    public TutorProfile TutorProfile { get; set; } = default!;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }

    // How many students can book this slot. 1 = normal 1:1 session,
    // >1 = group session. IsBooked is kept as a maintained "is full" flag
    // (recomputed after every booking change) so existing "!IsBooked"
    // availability filters keep working unchanged.
    public int Capacity { get; set; } = 1;
    public bool IsBooked { get; set; }
}