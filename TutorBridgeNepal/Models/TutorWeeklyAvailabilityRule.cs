namespace TutorBridgeNepal.Models;

// One row per day of week per tutor (7 rows once set up). This is the
// template the slot-generation engine reads to create real, bookable
// TutorAvailabilitySlot rows going forward.
public class TutorWeeklyAvailabilityRule
{
    public int Id { get; set; }
    public int TutorProfileId { get; set; }
    public TutorProfile TutorProfile { get; set; } = default!;

    public DayOfWeek DayOfWeek { get; set; }
    public bool IsDayOff { get; set; } = true;
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
}