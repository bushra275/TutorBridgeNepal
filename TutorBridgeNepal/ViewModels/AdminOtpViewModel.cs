using System.ComponentModel.DataAnnotations;

namespace TutorBridgeNepal.ViewModels;

public class AdminOtpViewModel
{
    [Required]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter the 6-digit code we emailed you.")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "The code should be exactly 6 digits.")]
    public string Code { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
}