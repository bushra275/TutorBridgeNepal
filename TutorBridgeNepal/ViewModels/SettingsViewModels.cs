namespace TutorBridgeNepal.ViewModels;

public class SettingsPageViewModel
{
    public string Initials { get; set; } = string.Empty;
    public SettingsProfileFormModel Profile { get; set; } = new();
    public SettingsAcademicFormModel Academic { get; set; } = new();
    public SettingsNotificationsModel Notifications { get; set; } = new();
    public bool ShowProfileToTutors { get; set; }
}

public class SettingsProfileFormModel
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? GradeLevel { get; set; }
    public string? District { get; set; }
}

public class SettingsAcademicFormModel
{
    public string? SchoolName { get; set; }
    public string? CurriculumBoard { get; set; }
    public string? SubjectsEnrolled { get; set; }
}

public class SettingsNotificationsModel
{
    public bool SessionReminders { get; set; }
    public bool NewMessages { get; set; }
    public bool ProgressUpdates { get; set; }
}