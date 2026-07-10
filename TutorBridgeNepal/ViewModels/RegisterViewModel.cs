using System.ComponentModel.DataAnnotations;

namespace TutorBridgeNepal.ViewModels;

public class RegisterViewModel
{
    [Required]
    public string Role { get; set; } = "Student";

    [Required]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }
    public string? District { get; set; }
    public string? GradeLevel { get; set; }
    public string? Subjects { get; set; }
    public int YearsOfExperience { get; set; }

    [Required, MinLength(6)]
    public string Password { get; set; } = string.Empty;

    [Compare(nameof(Password))]
    public string ConfirmPassword { get; set; } = string.Empty;
}