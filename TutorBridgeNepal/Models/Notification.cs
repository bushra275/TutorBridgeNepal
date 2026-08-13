namespace TutorBridgeNepal.Models;

// One row per real platform event an admin might care about. Created at the
// exact moment the underlying event happens (see NotificationHelper and its
// call sites in AccountController/TutorController/StudentController/
// AdminController) - never synthesized or backfilled from guesses, only from
// events that actually occurred.
public class Notification
{
    public int Id { get; set; }

    // "Verification" (tutor submitted/approved/rejected), "Complaint"
    // (filed/resolved), "System" (registrations, session completions, and
    // any other general platform activity). Drives the tab filters on
    // Admin > Notifications.
    public string Type { get; set; } = "System";

    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Icon { get; set; } = "🔔";

    // Optional call-to-action shown as a button on the notification row.
    // Both null together means an informational, non-actionable row.
    public string? ActionLabel { get; set; }
    public string? ActionUrl { get; set; }

    public bool IsRead { get; set; }

    // Drives the red/pink highlight for things like a high-severity
    // complaint - set explicitly by the call site, not inferred.
    public bool IsHighPriority { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}