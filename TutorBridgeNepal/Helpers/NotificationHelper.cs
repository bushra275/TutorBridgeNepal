using TutorBridgeNepal.Data;
using TutorBridgeNepal.Models;

namespace TutorBridgeNepal.Helpers;

// Queues a Notification row on the given context. Doesn't call
// SaveChangesAsync itself - it piggybacks on whatever SaveChangesAsync the
// calling action already does for its own work, so triggering a
// notification never costs an extra database round trip.
public static class NotificationHelper
{
    public static void Create(
        ApplicationDbContext context,
        string type,
        string title,
        string message,
        string icon,
        string? actionLabel = null,
        string? actionUrl = null,
        bool isHighPriority = false)
    {
        context.Notifications.Add(new Notification
        {
            Type = type,
            Title = title,
            Message = message,
            Icon = icon,
            ActionLabel = actionLabel,
            ActionUrl = actionUrl,
            IsHighPriority = isHighPriority,
            CreatedAt = DateTime.Now
        });
    }
}