namespace TutorBridgeNepal.ViewModels;

public class ProgressPageViewModel
{
    public double TotalHoursLearned { get; set; }
    public double HoursLearnedThisWeek { get; set; }
    public int SessionsCompleted { get; set; }
    public int SessionsCompletedThisMonth { get; set; }
    public int SubjectsActiveCount { get; set; }
    public int CurrentStreakDays { get; set; }
    public int GoalCompletionPercent { get; set; }

    public List<SubjectProgressCardViewModel> SubjectCards { get; set; } = new();
    public List<WeeklyHoursPointViewModel> WeeklyHours { get; set; } = new();
    public List<MonthlyTrendSeriesViewModel> MonthlyTrend { get; set; } = new();
    public List<GoalRowViewModel> Goals { get; set; } = new();
    public List<AchievementViewModel> Achievements { get; set; } = new();
}

public class SubjectProgressCardViewModel
{
    public string Subject { get; set; } = string.Empty;
    public string TutorName { get; set; } = string.Empty;
    public int SessionsCount { get; set; }
    public double HoursLearned { get; set; }

    // % of this subject's booked (non-cancelled) sessions that were completed.
    // This is a real, computable number - NOT a mastery/topic score.
    public int CompletionRatePercent { get; set; }
}

public class WeeklyHoursPointViewModel
{
    public string DayLabel { get; set; } = string.Empty;
    public double Hours { get; set; }
}

public class MonthlyTrendSeriesViewModel
{
    public string Subject { get; set; } = string.Empty;
    public List<MonthlyTrendPointViewModel> Points { get; set; } = new();
}

public class MonthlyTrendPointViewModel
{
    public string MonthLabel { get; set; } = string.Empty;
    public int CompletedSessions { get; set; }
}

public class GoalRowViewModel
{
    public int GoalId { get; set; }
    public string? Subject { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "NotStarted";
    public DateTime? DueDate { get; set; }
}

public class AchievementViewModel
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public bool Unlocked { get; set; }
    public DateTime? UnlockedAt { get; set; }
    public string? LockedProgressText { get; set; }
}