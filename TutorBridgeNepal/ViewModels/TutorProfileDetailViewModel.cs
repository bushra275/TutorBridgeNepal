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
    public int ReviewCount { get; set; }
    public List<ReviewRowViewModel> Reviews { get; set; } = new();

    // The calendar month currently being displayed on the booking panel.
    // Defaults to the current month; navigated via ?month=&year= on the
    // TutorProfile action so students can actually reach slots that fall
    // in a later month instead of being stuck looking at only whichever
    // month the very first available slot happens to fall in.
    public DateTime DisplayMonth { get; set; }

    // Set only when this ViewModel is reused for the RescheduleBooking page
    // instead of the normal TutorProfile page - identifies which existing
    // booking is being moved, and what its current time slot is, so the
    // view can show "currently booked for..." and the subject stays fixed
    // instead of offering a subject picker.
    public int? RescheduleBookingId { get; set; }
    public string? RescheduleSubject { get; set; }
    public DateTime? CurrentSlotStartTime { get; set; }
    public DateTime? CurrentSlotEndTime { get; set; }

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

public class ReviewRowViewModel
{
    public string StudentName { get; set; } = string.Empty;
    public string StudentInitials { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? TutorReply { get; set; }
    public DateTime? TutorRepliedAt { get; set; }
}