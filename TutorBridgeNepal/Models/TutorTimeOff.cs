namespace TutorBridgeNepal.Models;

// A real blocked period ("Block time off"). The slot-generation engine
// skips creating slots inside these ranges, and existing unbooked slots
// inside a newly-added range are removed.
public class TutorTimeOff
{
    public int Id { get; set; }
    public int TutorProfileId { get; set; }
    public TutorProfile TutorProfile { get; set; } = default!;

    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}