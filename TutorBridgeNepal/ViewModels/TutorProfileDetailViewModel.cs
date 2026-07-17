namespace TutorBridgeNepal.ViewModels;

public class TutorProfileDetailViewModel
{
    public int TutorProfileId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public string Subjects { get; set; } = string.Empty;
    public string? District { get; set; }
    public string? Bio { get; set; }
    public string? TeachingStyle { get; set; }
    public int YearsOfExperience { get; set; }
    public decimal AverageRating { get; set; }
    public bool IsVerified { get; set; }
    public int CompletedSessionsCount { get; set; }

    public List<string> SubjectTags =>
        Subjects.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    public List<string> TeachingStyleTags =>
        (TeachingStyle ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    public List<AvailableSlotViewModel> AvailableSlots { get; set; } = new();

    public List<AvailableDateGroup> SlotsByDate =>
        AvailableSlots
            .GroupBy(s => s.StartTime.Date)
            .OrderBy(g => g.Key)
            .Select(g => new AvailableDateGroup
            {
                Date = g.Key,
                Slots = g.OrderBy(s => s.StartTime).ToList()
            })
            .ToList();
}

public class AvailableSlotViewModel
{
    public int SlotId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}

public class AvailableDateGroup
{
    public DateTime Date { get; set; }
    public List<AvailableSlotViewModel> Slots { get; set; } = new();
}