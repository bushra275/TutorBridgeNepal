namespace TutorBridgeNepal.Models;

// Single source of truth for achievement display metadata, shared by the
// Progress page (which checks/awards them) and the notification bell
// (which reports recently-unlocked ones). Keeps the two in sync.
public static class AchievementCatalog
{
    public static readonly Dictionary<string, (string Title, string Icon)> Items = new()
    {
        ["sessions_10"] = ("10 sessions", "🎯"),
        ["streak_7"] = ("7-day streak", "🔥"),
        ["top_student_month"] = ("Top student", "⭐"),
        ["sessions_25"] = ("25 sessions", "🏅"),
    };
}