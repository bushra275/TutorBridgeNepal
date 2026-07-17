namespace TutorBridgeNepal.Models;

public class TutorProfile
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = default!;
    public string Subjects { get; set; } = string.Empty;
    public int YearsOfExperience { get; set; }
    public string? Bio { get; set; }
    public string? TeachingStyle { get; set; }
    public bool IsVerified { get; set; }
    public decimal AverageRating { get; set; }
}