namespace TutorBridgeNepal.Models;

// Singleton row (there is always exactly one) holding the platform-wide
// toggles shown on Admin > Settings > Platform configuration. Read via
// AdminController.GetPlatformSettingsAsync(), which creates the row with
// its defaults the first time it's needed.
//
// Not every flag here is wired into real enforcement yet - see the comment
// on each property for what actually happens today versus what's still
// just "saved for later".
public class PlatformSettings
{
    public int Id { get; set; }

    // NOT YET ENFORCED. Saved so the value survives, but nothing currently
    // reads it to skip manual tutor review - every application still goes
    // through the normal Tutor Verification queue regardless of this flag.
    public bool AutoApproveVerifiedTutors { get; set; } = false;

    // ENFORCED: AdminController.ApproveTutor refuses to approve a tutor who
    // is missing a PoliceReport credential while this is true.
    public bool RequirePoliceReportForTutors { get; set; } = true;

    // NOT YET ENFORCED. Saved so the value survives, but session booking
    // (StudentController) does not currently check this flag - the
    // existing minimum-notice rule is still only the individual tutor's
    // own MinimumBookingNoticeHours setting.
    public bool AllowSameDayBooking { get; set; } = true;

    // ENFORCED: Program.cs checks this on every request (except /Admin and
    // /Account) and serves a 503 maintenance page to signed-out users and
    // to any signed-in user who isn't an Admin.
    public bool PlatformMaintenanceMode { get; set; } = false;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}