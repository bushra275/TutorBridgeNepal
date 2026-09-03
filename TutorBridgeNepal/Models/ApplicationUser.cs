using Microsoft.AspNetCore.Identity;

namespace TutorBridgeNepal.Models;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public string? District { get; set; }
    public string? PhotoUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Admin-initiated account suspension (User Management page). Blocks
    // login while set - separate from TutorProfile.IsDeactivated, which is
    // the tutor's own self-service pause.
    public bool IsSuspended { get; set; }

    // One-time 6-digit code emailed at registration to confirm the address
    // is real and reachable. Cleared once used. Short-lived (see
    // EmailOtpExpiresAt) and single-use - a fresh code invalidates any
    // earlier one.
    public string? EmailOtpCode { get; set; }
    public DateTime? EmailOtpExpiresAt { get; set; }
}