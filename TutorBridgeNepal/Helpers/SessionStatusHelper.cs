using TutorBridgeNepal.Models;

namespace TutorBridgeNepal.Helpers;

// Centralizes the "Confirmed -> Ongoing -> Completed/Missed" state machine
// for a booking so both StudentController and TutorController apply the
// exact same rules.
//
// There's no background job runner in this app, so "a session nobody
// joined has now ended, mark it Missed" can't fire on a timer. Instead
// AutoMarkMissed is called at the top of every action that loads a list of
// a user's bookings (dashboards, schedules, session lists) and lazily
// applies the rule to whatever was just loaded - the same pattern already
// used by AdminController.ComputeSessionDisplayStatus, just persisted
// instead of only affecting what's displayed.
public static class SessionStatusHelper
{
    // How long before a session starts the "Join" button lights up (and the
    // reminder notification goes out). Single source of truth so the two
    // controllers and the two views can't drift out of sync with each other.
    public const int JoinWindowMinutesBeforeStart = 15;

    // True while the join room is reachable for this booking: from
    // JoinWindowMinutesBeforeStart before the slot starts, up until it ends.
    public static bool CanJoin(Booking booking)
    {
        if (string.IsNullOrWhiteSpace(booking.MeetingLink)) return false;
        if (booking.Status != "Confirmed" && booking.Status != "Ongoing") return false;

        var now = DateTime.Now;
        var start = booking.TutorAvailabilitySlot.StartTime;
        var end = booking.TutorAvailabilitySlot.EndTime;
        return now >= start.AddMinutes(-JoinWindowMinutesBeforeStart) && now <= end;
    }

    // A session that reached "Ongoing" had both parties actually join, so
    // it's a real session - it's left for the tutor to explicitly mark
    // completed (or missed) even if that happens late. Only a "Confirmed"
    // booking whose slot has now ended without ever becoming Ongoing is a
    // genuine no-show from at least one side.
    public static bool AutoMarkMissed(IEnumerable<Booking> bookings)
    {
        var now = DateTime.Now;
        var changed = false;

        foreach (var b in bookings)
        {
            if (b.Status == "Confirmed" && b.TutorAvailabilitySlot != null && b.TutorAvailabilitySlot.EndTime <= now)
            {
                b.Status = "Missed";
                changed = true;
            }
        }

        return changed;
    }

    // Records that one side just opened the meeting room, and promotes the
    // booking to "Ongoing" the moment both sides have. Returns true if
    // anything on the booking changed (caller should save).
    public static bool RecordJoin(Booking booking, bool isStudent)
    {
        var changed = false;

        if (isStudent)
        {
            if (booking.StudentJoinedAt == null)
            {
                booking.StudentJoinedAt = DateTime.Now;
                changed = true;
            }
        }
        else
        {
            if (booking.TutorJoinedAt == null)
            {
                booking.TutorJoinedAt = DateTime.Now;
                changed = true;
            }
        }

        if (booking.Status == "Confirmed" && booking.StudentJoinedAt.HasValue && booking.TutorJoinedAt.HasValue)
        {
            booking.Status = "Ongoing";
            changed = true;
        }

        return changed;
    }
}