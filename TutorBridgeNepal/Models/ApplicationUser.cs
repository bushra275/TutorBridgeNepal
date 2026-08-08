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
}