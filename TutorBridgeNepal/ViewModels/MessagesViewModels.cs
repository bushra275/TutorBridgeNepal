namespace TutorBridgeNepal.ViewModels;

public class ConversationListItemViewModel
{
    public int TutorProfileId { get; set; }
    public string TutorName { get; set; } = string.Empty;
    public string TutorInitials { get; set; } = string.Empty;
    public string Subjects { get; set; } = string.Empty;
    public string? LastMessagePreview { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public int UnreadCount { get; set; }
}

public class MessageBubbleViewModel
{
    public int Id { get; set; }
    public string SenderRole { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public bool IsRead { get; set; }
}

public class MessagesPageViewModel
{
    public List<ConversationListItemViewModel> Conversations { get; set; } = new();

    public int? ActiveTutorProfileId { get; set; }
    public string? ActiveTutorName { get; set; }
    public string? ActiveTutorInitials { get; set; }
    public string? ActiveTutorSubjects { get; set; }

    public List<MessageBubbleViewModel> Messages { get; set; } = new();
    public int TotalUnread { get; set; }
}