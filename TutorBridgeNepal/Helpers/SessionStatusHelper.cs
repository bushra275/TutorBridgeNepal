using TutorBridgeNepal.Models;

namespace TutorBridgeNepal.Helpers;

// Centralizes the "Confirmed -> Ongoing -> Ended -> Completed/Missed"
// state machine for a booking so both StudentController and
// TutorController apply the exact same rules.
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

    // Called when a party opens the join room. Records that side's join
    // time, and promotes "Confirmed" -> "Ongoing" (stamping OngoingAt) the
    // moment both sides have joined. Returns true if the booking changed
    // (caller should save).
    public static bool RecordJoin(Booking booking, bool isStudent)
    {
        var changed = false;

        if (isStudent && booking.StudentJoinedAt == null)
        {
            booking.StudentJoinedAt = DateTime.Now;
            changed = true;
        }
        else if (!isStudent && booking.TutorJoinedAt == null)
        {
            booking.TutorJoinedAt = DateTime.Now;
            changed = true;
        }

        if (booking.Status == "Confirmed" && booking.StudentJoinedAt.HasValue && booking.TutorJoinedAt.HasValue)
        {
            booking.Status = "Ongoing";
            booking.OngoingAt = DateTime.Now;
            changed = true;
        }

        return changed;
    }

    // Called when the tutor clicks "Start session". Unlike RecordJoin, this
    // alone is enough to make the session live - the tutor is always the
    // one who starts it, and the student can't get into the room until
    // this has happened (see CanStudentJoin).
    public static bool StartSession(Booking booking)
    {
        if (booking.Status != "Confirmed") return false;

        booking.TutorJoinedAt ??= DateTime.Now;
        booking.Status = "Ongoing";
        booking.OngoingAt = DateTime.Now;
        return true;
    }

    // Whether the STUDENT specifically can enter the room right now. Unlike
    // CanJoin (used for the tutor's own "Start session" button), a student
    // can never get in before the tutor has actually started the session -
    // there's no time-window check here because the tutor's own start
    // action already establishes that it's an appropriate time.
    public static bool CanStudentJoin(Booking booking)
    {
        if (string.IsNullOrWhiteSpace(booking.MeetingLink)) return false;
        if (booking.Status != "Ongoing") return false;
        return DateTime.Now <= booking.TutorAvailabilitySlot.EndTime;
    }

    // Called when the client-side call UI detects the video call has ended
    // (hangup button, tab closed, Jitsi's demo-server disconnect, etc). Only
    // meaningful from "Ongoing" - a session that's already Ended/Completed/
    // Missed shouldn't be reopened by a stray late event. Returns true if
    // the booking changed (caller should save).
    public static bool EndCall(Booking booking)
    {
        if (booking.Status != "Ongoing") return false;

        booking.Status = "Ended";
        booking.CallEndedAt = DateTime.Now;
        return true;
    }

    // How long the call actually ran, from the moment it went live
    // (OngoingAt) to the moment it was detected as ended (CallEndedAt).
    // Null if the session never actually went Ongoing, or hasn't ended yet.
    public static TimeSpan? GetCallDuration(Booking booking)
    {
        if (booking.OngoingAt == null || booking.CallEndedAt == null) return null;
        var duration = booking.CallEndedAt.Value - booking.OngoingAt.Value;
        return duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
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

        foreach (var booking in bookings)
        {
            if (booking.Status == "Confirmed" && booking.TutorAvailabilitySlot.EndTime <= now)
            {
                booking.Status = "Missed";
                changed = true;
            }
        }

        return changed;
    }
}