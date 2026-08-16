namespace TutorBridgeNepal.ViewModels;

public class AdminSettingsPageViewModel
{
    public string AdminName { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }

    public AdminProfileFormModel Profile { get; set; } = new();

    public bool TwoFactorEnabled { get; set; }

    public AdminPlatformConfigModel PlatformConfig { get; set; } = new();

    // Sole-admin safeguard: Deactivate/Delete in the Danger zone are
    // disabled when this admin is the only one left, so the platform can
    // never be locked out of its own admin console.
    public int AdminCount { get; set; }
    public bool IsSoleAdmin => AdminCount <= 1;
}

public class AdminProfileFormModel
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
}

public class AdminPlatformConfigModel
{
    public bool AutoApproveVerifiedTutors { get; set; }
    public bool RequirePoliceReportForTutors { get; set; }
    public bool AllowSameDayBooking { get; set; }
    public bool PlatformMaintenanceMode { get; set; }
}