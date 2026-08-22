namespace TutorBridgeNepal.Models;

// One row per tutor who has connected Google Calendar. Tokens are stored
// as-is here for FYP scope - a production version would encrypt these
// (e.g. with ASP.NET Core Data Protection) before saving.
public class TutorCalendarConnection
{
    public int Id { get; set; }

    public int TutorProfileId { get; set; }
    public TutorProfile TutorProfile { get; set; } = default!;

    public string Provider { get; set; } = "Google";
    public string GoogleAccountEmail { get; set; } = string.Empty;

    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime AccessTokenExpiresAt { get; set; }

    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
}