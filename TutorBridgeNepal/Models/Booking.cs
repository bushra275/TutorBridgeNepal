namespace TutorBridgeNepal.Models;

public class Booking
{
    public int Id { get; set; }
    public int StudentProfileId { get; set; }
    public StudentProfile StudentProfile { get; set; } = default!;
    public int TutorProfileId { get; set; }
    public TutorProfile TutorProfile { get; set; } = default!;
    public int TutorAvailabilitySlotId { get; set; }
    public TutorAvailabilitySlot TutorAvailabilitySlot { get; set; } = default!;
    public string Subject { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // When the tutor actually accepted/declined the request (null while still
    // pending). Distinct from CreatedAt so real response-time metrics work.
    public DateTime? DecidedAt { get; set; }

    // Both a tutor decline and a student cancellation set Status = "Cancelled" -
    // this flag is the only way to tell them apart afterward.
    public bool DeclinedByTutor { get; set; }

    // Optional message the student can attach when requesting a session.
    public string? Note { get; set; }
    // True while a linked SupportTicket (Category "Booking") on this session
    // is still open. Set by Tutor/StudentController.SubmitSupportTicket when
    // a ticket references this booking, or directly by an admin via
    // AdminController.FlagSessionDisputed. Cleared by
    // AdminController.ResolveDispute once the admin has handled it.
    public bool IsDisputed { get; set; }

    // Google Calendar event id for this session, set once the tutor accepts
    // the request and it gets pushed to their connected calendar. Null if
    // they've never connected Google Calendar, or the push failed.
    public string? GoogleCalendarEventId { get; set; }

    // In-app video call room, generated once the booking becomes Confirmed
    // (either by the tutor accepting it, or immediately for auto-accepted
    // returning students). Null until then.
    public string? MeetingLink { get; set; }

    public DateTime? StudentJoinedAt { get; set; }
    public DateTime? TutorJoinedAt { get; set; }

    // Set once the "your session starts in 15 minutes" reminder email/
    // notification has gone out to both parties, so SessionReminderService
    // never sends it twice for the same booking.
    public DateTime? ReminderSentAt { get; set; }

    public DateTime? OngoingAt { get; set; }
    public DateTime? CallEndedAt { get; set; }
}