namespace TutorBridgeNepal.ViewModels;

public class NotificationItemViewModel
{
    public string Type { get; set; } = string.Empty; // "Message", "Session", "Achievement", "Goal"
    public string Icon { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string LinkController { get; set; } = "Student";
    public string LinkAction { get; set; } = "Dashboard";
    public int? RouteId { get; set; } // e.g. tutorProfileId, when the link needs it
}