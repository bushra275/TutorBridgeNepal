namespace TutorBridgeNepal.Models;

// One row in the "Credentials & documents" panel (e.g. "B.Ed Mathematics",
// "Citizenship.pdf"). Read-only in the UI for now, since there is no file
// upload pipeline yet - rows exist in the DB only (seeded or added directly).
public class TutorCredential
{
    public int Id { get; set; }
    public int TutorProfileId { get; set; }
    public TutorProfile TutorProfile { get; set; } = default!;

    public string Title { get; set; } = string.Empty;
    public string? FileName { get; set; }

    // Small emoji shown in the chip, e.g. "🎓" for a degree, "📄" for a
    // generic document, "🪪" for an ID document.
    public string Icon { get; set; } = "📄";
    public int SortOrder { get; set; }
}   