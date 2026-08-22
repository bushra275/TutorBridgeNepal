namespace TutorBridgeNepal.Models;

// One row per signed-in browser session. Created at login with a random
// SessionToken that's also dropped into a small non-Identity cookie
// ("tbn_device"), so we can tell which row is "this device" and revoke a
// single session without touching the others - unlike SignOutAllDevices,
// which is all-or-nothing via the security stamp.
public class UserDevice
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public string SessionToken { get; set; } = string.Empty;

    // Parsed from the User-Agent header at sign-in, e.g. "Chrome on Windows".
    public string DeviceLabel { get; set; } = "Unknown device";
    public string? IpAddress { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;

    public bool IsRevoked { get; set; }
}