namespace TutorBridgeNepal.ViewModels;

public class TutorSettingsPageViewModel
{
    public string Initials { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public bool TwoFactorEnabled { get; set; }

    public bool ShowAvailabilityBadge { get; set; }
    public bool AutoAcceptReturningStudents { get; set; }

    public int MinimumBookingNoticeHours { get; set; }
    public int CancellationWindowHours { get; set; }
    public int MaxSessionsPerDay { get; set; }

    public bool NotifyNewSessionRequests { get; set; }
    public bool NotifyNewMessages { get; set; }
    public bool IsListedInSearch { get; set; }

    public List<DeviceViewModel> Devices { get; set; } = new();
    public bool GoogleCalendarConnected { get; set; }
    public string? GoogleCalendarEmail { get; set; }
}

public class DeviceViewModel
{
    public int Id { get; set; }
    public string DeviceLabel { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastActiveAt { get; set; }
    public bool IsCurrentDevice { get; set; }
}