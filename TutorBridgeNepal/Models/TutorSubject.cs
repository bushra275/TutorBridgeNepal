namespace TutorBridgeNepal.Models;

public class TutorSubject
{
    public int Id { get; set; }
    public int TutorProfileId { get; set; }
    public TutorProfile TutorProfile { get; set; } = default!;

    public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
}