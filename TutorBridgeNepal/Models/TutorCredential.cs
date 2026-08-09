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

    public string? FilePath { get; set; }

    // Size of the uploaded file in bytes, as recorded at upload time.
    public long FileSizeBytes { get; set; }

    // Small emoji shown in the chip, e.g. "🎓" for a degree, "📄" for a
    // generic document, "🪪" for an ID document.
    public string Icon { get; set; } = "📄";
    public int SortOrder { get; set; }
    public DateTime UploadedAt { get; set; }

    // Tags a row as one of the four documents the verification checklist
    // looks for: "Citizenship", "CVResume", "DegreeCertificate",
    // "PoliceReport". Null for rows that are general credentials (e.g. a
    // listed degree title) rather than one of the required checklist
    // documents. Lets the Tutor Verification page detect which required
    // documents are missing for a given applicant instead of guessing from
    // the title text.
    public string? DocumentType { get; set; }
}