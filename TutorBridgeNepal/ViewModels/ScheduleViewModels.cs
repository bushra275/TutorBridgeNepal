namespace TutorBridgeNepal.ViewModels;

public class SchedulePageViewModel
{
    public string ViewMode { get; set; } = "week";
    public DateTime AnchorDate { get; set; }
    public DateTime RangeStart { get; set; }
    public string RangeLabel { get; set; } = string.Empty;

    public List<ScheduleDayViewModel> Days { get; set; } = new();
    public List<WeeklyAvailabilityDayViewModel> WeeklyAvailability { get; set; } = new();
    public List<TimeOffRowViewModel> UpcomingTimeOff { get; set; } = new();

    public int SessionsScheduledCount { get; set; }
    public int PendingRequestsCount { get; set; }
    public int BlockedDaysCount { get; set; }
    public int MissedSessionsCount { get; set; }
}

public class ScheduleDayViewModel
{
    public DateTime Date { get; set; }
    public bool IsCurrentPeriod { get; set; } = true;
    public bool IsToday { get; set; }
    public bool IsDayOff { get; set; }
    public List<ScheduleEventViewModel> Events { get; set; } = new();
    public List<TimeOffRowViewModel> BlockedRanges { get; set; } = new();
}

public class ScheduleEventViewModel
{
    public int SlotId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int Capacity { get; set; }
    public List<ScheduleBookingRowViewModel> Bookings { get; set; } = new();
    public bool IsGroup => Capacity > 1;
}

public class ScheduleBookingRowViewModel
{
    public int BookingId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class WeeklyAvailabilityDayViewModel
{
    public DayOfWeek DayOfWeek { get; set; }
    public string DayLabel { get; set; } = string.Empty;
    public bool IsDayOff { get; set; } = true;
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
}

public class TimeOffRowViewModel
{
    public int Id { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public string? Reason { get; set; }
}