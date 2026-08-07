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
    public bool NotifyWeeklyEarningsSummary { get; set; }

    // Privacy - whether this tutor appears in student search results
    public bool IsListedInSearch { get; set; } = true;

    // Deactivation - reversible-in-spirit account pause; blocks login while set
    public bool IsDeactivated { get; set; }
}