namespace TutorBridgeNepal.ViewModels;

public class TutorMessagesPageViewModel
{
    public string Tab { get; set; } = "all";
    public string? Search { get; set; }
    public List<TutorConversationListItemViewModel> Conversations { get; set; } = new();
    public int TotalUnread { get; set; }

    public int? ActiveStudentProfileId { get; set; }
    public string ActiveStudentName { get; set; } = string.Empty;
    public string ActiveStudentInitials { get; set; } = string.Empty;
    public string? ActiveStudentGradeLevel { get; set; }
    public List<string> ActiveStudentSubjects { get; set; } = new();
    public bool ActiveStudentIsActive { get; set; }
    public bool ActiveStudentIsNew { get; set; }
    public List<TutorMessageBubbleViewModel> Messages { get; set; } = new();
}

public class TutorConversationListItemViewModel
{
    public int StudentProfileId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentInitials { get; set; } = string.Empty;
    public string? LastMessagePreview { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public int UnreadCount { get; set; }
    public List<string> Subjects { get; set; } = new();
    public bool IsNew { get; set; }
    public DateTime? NextSessionAt { get; set; }
    public DateTime? LastSessionAt { get; set; }
}

public class TutorMessageBubbleViewModel
{
    public int Id { get; set; }
    public string SenderRole { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public bool IsRead { get; set; }
}