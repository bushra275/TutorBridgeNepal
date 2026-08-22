using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TutorBridgeNepal.Data;
using TutorBridgeNepal.Models;

namespace TutorBridgeNepal.Services;

// Talks to Google's Calendar REST API directly rather than pulling in the
// full Google.Apis SDK - keeps the dependency footprint small for what's
// really just "create an event, delete an event".
public class GoogleCalendarService
{
    private readonly HttpClient _http;
    private readonly ApplicationDbContext _context;
    private readonly GoogleOAuthOptions _options;

    public GoogleCalendarService(HttpClient http, ApplicationDbContext context, IOptions<GoogleOAuthOptions> options)
    {
        _http = http;
        _context = context;
        _options = options.Value;
    }

    public async Task<(bool Success, string? EventId)> CreateEventAsync(TutorCalendarConnection connection, Booking booking)
    {
        var accessToken = await GetValidAccessTokenAsync(connection);
        if (accessToken == null) return (false, null);

        var studentName = booking.StudentProfile?.User?.FullName ?? "Student";
        var payload = new
        {
            summary = $"TutorBridge session - {booking.Subject}",
            description = $"TutorBridge Nepal session with {studentName}.",
            start = new { dateTime = booking.TutorAvailabilitySlot.StartTime.ToString("o"), timeZone = "Asia/Kathmandu" },
            end = new { dateTime = booking.TutorAvailabilitySlot.EndTime.ToString("o"), timeZone = "Asia/Kathmandu" }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "https://www.googleapis.com/calendar/v3/calendars/primary/events")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return (false, null);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (true, doc.RootElement.GetProperty("id").GetString());
    }

    public async Task DeleteEventAsync(TutorCalendarConnection connection, string eventId)
    {
        var accessToken = await GetValidAccessTokenAsync(connection);
        if (accessToken == null) return;

        var request = new HttpRequestMessage(HttpMethod.Delete, $"https://www.googleapis.com/calendar/v3/calendars/primary/events/{eventId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        // A 404/410 just means the event is already gone (e.g. the tutor
        // deleted it by hand from their own calendar) - nothing left to do.
        await _http.SendAsync(request);
    }

    private async Task<string?> GetValidAccessTokenAsync(TutorCalendarConnection connection)
    {
        if (connection.AccessTokenExpiresAt > DateTime.UtcNow.AddMinutes(1))
        {
            return connection.AccessToken;
        }

        var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["refresh_token"] = connection.RefreshToken,
                ["grant_type"] = "refresh_token"
            })
        };

        var response = await _http.SendAsync(refreshRequest);
        if (!response.IsSuccessStatusCode) return null;

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        connection.AccessToken = root.GetProperty("access_token").GetString() ?? connection.AccessToken;
        connection.AccessTokenExpiresAt = DateTime.UtcNow.AddSeconds(root.GetProperty("expires_in").GetInt32());
        await _context.SaveChangesAsync();

        return connection.AccessToken;
    }
}