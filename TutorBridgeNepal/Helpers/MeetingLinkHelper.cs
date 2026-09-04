namespace TutorBridgeNepal.Helpers;

// Generates a unique, embeddable video-call room for a confirmed session.
// Uses Jitsi Meet's free public server (meet.jit.si) - no API key, account,
// or paid plan needed. Anyone with the exact room URL can join, so the room
// name embeds a short random token alongside the booking id to make it
// unguessable.
public static class MeetingLinkHelper
{
    public static string GenerateForBooking(int bookingId)
    {
        var token = Guid.NewGuid().ToString("N")[..10];
        return $"https://meet.jit.si/TutorBridgeNepal-{bookingId}-{token}";
    }
}