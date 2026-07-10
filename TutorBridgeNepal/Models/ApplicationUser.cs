using Microsoft.AspNetCore.Identity;

namespace TutorBridgeNepal.Models;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public string? District { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}