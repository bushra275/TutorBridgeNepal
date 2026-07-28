namespace TutorBridgeNepal.Models;

// One row in the "Subjects & rates" panel on the tutor's My Profile page
// (e.g. "Mathematics - Grade 9-12, SEE Prep, +2 - Rs 500/hr").
public class TutorSubjectRate
{
    public int Id { get; set; }
    public int TutorProfileId { get; set; }
    public TutorProfile TutorProfile { get; set; } = default!;

    public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal RatePerHour { get; set; }
    public int SortOrder { get; set; }
}