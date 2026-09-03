using System.ComponentModel.DataAnnotations;

namespace TutorBridgeNepal.ViewModels;

public class RegisterViewModel
{
    [Required]
    public string Role { get; set; } = "Student";

    [Required]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone number is required.")]
    [RegularExpression(@"^9[678]\d{8}$", ErrorMessage = "Enter a valid 10-digit Nepali mobile number (e.g. 98XXXXXXXX).")]
    public string PhoneNumber { get; set; } = string.Empty;
    public string? District { get; set; }
    public string? GradeLevel { get; set; }
    public string? Subjects { get; set; }
    public int YearsOfExperience { get; set; }

    [Required, MinLength(6)]
    public string Password { get; set; } = string.Empty;

    [Compare(nameof(Password))]
    public string ConfirmPassword { get; set; } = string.Empty;
}