namespace TutorBridgeNepal.Models;

public class TutorProfile
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = default!;
    public string Subjects { get; set; } = string.Empty;
    public int YearsOfExperience { get; set; }
    public string? Bio { get; set; }
    public string? TeachingStyle { get; set; }

    // Optional public-facing name shown instead of FullName (e.g. "Ram Sir").
    public string? DisplayName { get; set; }

    // --- Verification application fields (Admin > Tutor Verification) ---

    // Free text, same storage pattern as Bio/TeachingStyle, e.g.
    // "B.Sc Chemistry, Tribhuvan University (2021)". Shown on the
    // verification review card; entered by the tutor at registration/profile
    // edit time, reviewed (not edited) by the admin.
    public string? Education { get; set; }

    // Free text summary of prior tutoring/teaching experience, e.g.
    // "3 years private tutoring · Grade 9-12, NEB Board". Distinct from
    // YearsOfExperience (a number used across the platform for search/
    // sorting) - this is the human-readable sentence shown to reviewers.
    public string? ExperienceSummary { get; set; }

    // Set the moment an admin approves or rejects the application (see
    // AdminController.ApproveTutor/RejectTutor). Null while pending. Used
    // together with User.CreatedAt (treated as the submission date) to
    // compute review turnaround time and this month's approved/rejected
    // counts on the Tutor Verification page.
    public DateTime? VerificationDecidedAt { get; set; }

    // e.g. "Online & In-person" - free text, matches the select options in the view.
    public string? TeachingMode { get; set; }

    // Comma-separated, same storage pattern as TeachingStyle/Subjects.
    public string? Languages { get; set; }

    public bool IsVerified { get; set; }

    // Set when an admin rejects a pending application (as opposed to just not
    // having reviewed it yet) so rejected tutors drop out of the verification
    // queue instead of reappearing as still-pending.
    public bool VerificationRejected { get; set; }
    public decimal AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public bool IsAvailableNow { get; set; }

    // --- Settings page fields ---

    // Availability preferences
    public bool ShowAvailabilityBadge { get; set; } = true;
    public bool AutoAcceptReturningStudents { get; set; }

    // Booking & cancellation policy. 0 = "no limit"/"no minimum" for the count/hour fields.
    public int MinimumBookingNoticeHours { get; set; } = 12;
    public int CancellationWindowHours { get; set; } = 4;
    public int MaxSessionsPerDay { get; set; }

    // Notification preferences (stored for real; no email/push sender exists
    // yet, same "saved but not wired up" honesty as the student settings page)
    public bool NotifyNewSessionRequests { get; set; } = true;
    public bool NotifyNewMessages { get; set; } = true;

    // Privacy - whether this tutor appears in student search results
    public bool IsListedInSearch { get; set; } = true;

    // Deactivation - reversible-in-spirit account pause; blocks login while set
    // Deactivation - reversible-in-spirit account pause; blocks login while set
    public bool IsDeactivated { get; set; }

    // Set by Admin > Tutor Verification's "Request more info" action, shown
    // to the tutor on their VerificationPending page. Cleared automatically
    // the next time the tutor uploads a document (see
    // TutorController.UploadVerificationDocument), since a fresh upload is
    // the tutor's way of responding to the request.
    public string? VerificationNote { get; set; }
}