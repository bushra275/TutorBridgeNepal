using System.ComponentModel.DataAnnotations;

namespace TutorBridgeNepal.ViewModels;

public class LoginViewModel
{
    [Required]
    public string Role { get; set; } = "Student";

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
}