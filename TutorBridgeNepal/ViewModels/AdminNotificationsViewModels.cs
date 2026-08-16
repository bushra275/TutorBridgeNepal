namespace TutorBridgeNepal.ViewModels;

public class AdminNotificationsViewModel
{
    public string AdminName { get; set; } = string.Empty;
    public string AdminInitials { get; set; } = string.Empty;

    // ---- KPI strip ----
    public int UnreadCount { get; set; }
    public int RequiresActionCount { get; set; }
    public int TodayCount { get; set; }
    public int ThisWeekCount { get; set; }

    // ---- Tabs ----
    public string ActiveTab { get; set; } = "all";

    // ---- Filters ----
    public string? TypeFilter { get; set; }
    public string Sort { get; set; } = "newest";

    // ---- Results, grouped by day ----
    public List<NotificationDayGroupViewModel> Groups { get; set; } = new();
    public int VisibleCount { get; set; }
    public int TotalMatching { get; set; }
    public bool HasMore => TotalMatching > VisibleCount;
    public int RemainingCount => Math.Max(0, TotalMatching - VisibleCount);
}

public class NotificationDayGroupViewModel
{
    public string DayLabel { get; set; } = string.Empty;
    public List<NotificationRowViewModel> Items { get; set; } = new();
}

public class NotificationRowViewModel
{
    public int Id { get; set; }
    public string Type { get; set; } = "System";
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Icon { get; set; } = "🔔";
    public string? ActionLabel { get; set; }
    public string? ActionUrl { get; set; }
    public bool IsRead { get; set; }
    public bool IsHighPriority { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Lightweight shape used only by the topbar bell dropdown (_AdminNotifBell
// partial) - a trimmed-down view of Notification for the preview list, as
// opposed to NotificationRowViewModel which backs the full Notifications page.
public class AdminNotifBellItemViewModel
{
    public string Icon { get; set; } = "🔔";
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
}