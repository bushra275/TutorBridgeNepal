using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutorBridgeNepal.Data;
using TutorBridgeNepal.Models;
using TutorBridgeNepal.ViewModels;

namespace TutorBridgeNepal.Controllers;

[Authorize(Roles = "Tutor")]
public class TutorController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public TutorController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    private static string GetInitials(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "?";
        if (parts.Length == 1) return parts[0][..Math.Min(2, parts[0].Length)].ToUpper();
        return $"{parts[0][0]}{parts[^1][0]}".ToUpper();
    }

    private async Task<TutorProfile?> GetCurrentTutorProfileAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return null;
        return await _context.TutorProfiles.Include(t => t.User).FirstOrDefaultAsync(t => t.UserId == user.Id);
    }

    private async Task SetTutorSidebarContextAsync(string activeNav, TutorProfile? tutorProfile = null)
    {
        var tutor = tutorProfile ?? await GetCurrentTutorProfileAsync();
        if (tutor == null) return;

        var pendingCount = await _context.Bookings.CountAsync(b => b.TutorProfileId == tutor.Id && b.Status == "Pending");
        var unreadMessageCount = await _context.Messages.CountAsync(m =>
            m.TutorProfileId == tutor.Id && m.SenderRole == "Student" && !m.IsRead);

        ViewData["SidebarName"] = tutor.User.FullName;
        ViewData["SidebarInitials"] = GetInitials(tutor.User.FullName);
        ViewData["SidebarMeta"] = string.Join(" · ", new[] { tutor.Subjects.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault(), tutor.User.District }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
        ViewData["ActiveNav"] = activeNav;
        ViewData["PendingRequestCount"] = pendingCount;
        ViewData["UnreadMessageCount"] = unreadMessageCount;
        ViewData["IsAvailableNow"] = tutor.IsAvailableNow;
    }

    public async Task<IActionResult> Dashboard()
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");

        await SetTutorSidebarContextAsync("dashboard", tutor);

        var now = DateTime.Now;
        var today = DateTime.Today;

        var bookings = await _context.Bookings
            .Include(b => b.StudentProfile).ThenInclude(s => s.User)
            .Include(b => b.TutorAvailabilitySlot)
            .Where(b => b.TutorProfileId == tutor.Id)
            .ToListAsync();

        var nonCancelled = bookings.Where(b => b.Status != "Cancelled").ToList();
        var completed = bookings.Where(b => b.Status == "Completed").ToList();
        var pending = bookings.Where(b => b.Status == "Pending").OrderBy(b => b.CreatedAt).ToList();

        var todaySchedule = bookings
            .Where(b => b.Status == "Confirmed" && b.TutorAvailabilitySlot.StartTime.Date == today)
            .OrderBy(b => b.TutorAvailabilitySlot.StartTime)
            .ToList();

        var thisMonthStart = new DateTime(now.Year, now.Month, 1);
        var sessionsThisMonth = nonCancelled.Count(b => b.TutorAvailabilitySlot.StartTime >= thisMonthStart);

        double hoursTaught = completed
            .Sum(b => (b.TutorAvailabilitySlot.EndTime - b.TutorAvailabilitySlot.StartTime).TotalHours);

        var studentGroups = nonCancelled.GroupBy(b => b.StudentProfileId).ToList();
        var activeStudentsCount = studentGroups.Count;
        var newStudentsThisMonth = studentGroups.Count(g => g.Min(b => b.CreatedAt) >= thisMonthStart);

        TutorBookingRowViewModel ToRow(Booking b) => new()
        {
            BookingId = b.Id,
            StudentName = b.StudentProfile.User.FullName,
            StudentInitials = GetInitials(b.StudentProfile.User.FullName),
            StudentGradeLevel = b.StudentProfile.GradeLevel,
            Subject = b.Subject,
            StartTime = b.TutorAvailabilitySlot.StartTime,
            EndTime = b.TutorAvailabilitySlot.EndTime,
            Status = b.Status,
            RequestedAt = b.CreatedAt
        };

        var myStudents = studentGroups.Select(g =>
        {
            var studentBookings = g.OrderByDescending(b => b.TutorAvailabilitySlot.StartTime).ToList();
            var student = studentBookings.First().StudentProfile;
            var next = studentBookings
                .Where(b => b.TutorAvailabilitySlot.StartTime >= now && b.Status == "Confirmed")
                .OrderBy(b => b.TutorAvailabilitySlot.StartTime)
                .FirstOrDefault();

            return new TutorStudentRowViewModel
            {
                StudentProfileId = student.Id,
                FullName = student.User.FullName,
                Initials = GetInitials(student.User.FullName),
                GradeLevel = student.GradeLevel,
                SessionsCount = studentBookings.Count,
                NextSessionAt = next?.TutorAvailabilitySlot.StartTime
            };
        })
        .OrderByDescending(s => s.NextSessionAt.HasValue)
        .ThenByDescending(s => s.SessionsCount)
        .Take(6)
        .ToList();

        var recentReviews = await _context.Reviews
            .Include(r => r.StudentProfile).ThenInclude(s => s.User)
            .Where(r => r.TutorProfileId == tutor.Id)
            .OrderByDescending(r => r.CreatedAt)
            .Take(5)
            .Select(r => new TutorReviewRowViewModel
            {
                StudentName = r.StudentProfile.User.FullName,
                StudentInitials = GetInitials(r.StudentProfile.User.FullName),
                Rating = r.Rating,
                Comment = r.Comment,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync();

        var vm = new TutorDashboardViewModel
        {
            FullName = tutor.User.FullName,
            Initials = GetInitials(tutor.User.FullName),
            Subjects = tutor.Subjects,
            District = tutor.User.District,
            AverageRating = tutor.AverageRating,
            ReviewCount = tutor.ReviewCount,
            IsVerified = tutor.IsVerified,
            IsAvailableNow = tutor.IsAvailableNow,
            TotalSessions = nonCancelled.Count,
            TotalSessionsThisMonth = sessionsThisMonth,
            CompletedSessions = completed.Count,
            HoursTaught = Math.Round(hoursTaught, 1),
            ActiveStudentsCount = activeStudentsCount,
            NewStudentsThisMonth = newStudentsThisMonth,
            TodaySessionsCount = todaySchedule.Count,
            PendingRequestsCount = pending.Count,
            TodaySchedule = todaySchedule.Select(ToRow).ToList(),
            PendingRequests = pending.Select(ToRow).ToList(),
            RecentSessions = bookings
                .Where(b => b.Status == "Completed" || b.Status == "Cancelled")
                .OrderByDescending(b => b.TutorAvailabilitySlot.StartTime)
                .Take(5)
                .Select(ToRow)
                .ToList(),
            MyStudents = myStudents,
            RecentReviews = recentReviews
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcceptBooking(int id)
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");

        var booking = await _context.Bookings
            .FirstOrDefaultAsync(b => b.Id == id && b.TutorProfileId == tutor.Id && b.Status == "Pending");

        if (booking != null)
        {
            booking.Status = "Confirmed";
            await _context.SaveChangesAsync();
        }

        return RedirectToAction("Dashboard");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeclineBooking(int id)
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");

        var booking = await _context.Bookings
            .Include(b => b.TutorAvailabilitySlot)
            .FirstOrDefaultAsync(b => b.Id == id && b.TutorProfileId == tutor.Id && b.Status == "Pending");

        if (booking != null)
        {
            booking.Status = "Cancelled";

            var remainingActive = await _context.Bookings.CountAsync(b =>
                b.TutorAvailabilitySlotId == booking.TutorAvailabilitySlotId
                && b.Id != booking.Id && b.Status != "Cancelled");
            booking.TutorAvailabilitySlot.IsBooked = remainingActive >= booking.TutorAvailabilitySlot.Capacity;

            await _context.SaveChangesAsync();
        }

        return RedirectToAction("Dashboard");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkSessionCompleted(int id)
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");

        var booking = await _context.Bookings
            .Include(b => b.TutorAvailabilitySlot)
            .FirstOrDefaultAsync(b => b.Id == id && b.TutorProfileId == tutor.Id && b.Status == "Confirmed");

        // Can only mark a session complete once it has actually started - stops
        // a tutor from marking a future session done before it happens.
        if (booking != null && booking.TutorAvailabilitySlot.StartTime <= DateTime.Now)
        {
            booking.Status = "Completed";
            await _context.SaveChangesAsync();
        }

        return RedirectToAction("Dashboard");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleAvailability()
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");

        tutor.IsAvailableNow = !tutor.IsAvailableNow;
        await _context.SaveChangesAsync();

        return RedirectToAction("Dashboard");
    }
    private async Task GenerateUpcomingSlotsAsync(int tutorProfileId, int daysAhead = 30)
    {
        var rules = await _context.TutorWeeklyAvailabilityRules
            .Where(r => r.TutorProfileId == tutorProfileId)
            .ToListAsync();

        if (!rules.Any()) return; // tutor hasn't set up weekly availability yet

        var now = DateTime.Now;
        var horizon = DateTime.Today.AddDays(daysAhead);

        var timeOffs = await _context.TutorTimeOffs
            .Where(t => t.TutorProfileId == tutorProfileId && t.EndAt >= now)
            .ToListAsync();

        var existingStarts = (await _context.TutorAvailabilitySlots
            .Where(s => s.TutorProfileId == tutorProfileId && s.StartTime >= DateTime.Today)
            .Select(s => s.StartTime)
            .ToListAsync())
            .ToHashSet();

        var newSlots = new List<TutorAvailabilitySlot>();

        for (var date = DateTime.Today; date < horizon; date = date.AddDays(1))
        {
            var rule = rules.FirstOrDefault(r => r.DayOfWeek == date.DayOfWeek);
            if (rule == null || rule.IsDayOff || rule.StartTime == null || rule.EndTime == null) continue;

            var cursor = date.Add(rule.StartTime.Value);
            var dayEnd = date.Add(rule.EndTime.Value);

            while (cursor.AddHours(1) <= dayEnd)
            {
                var slotStart = cursor;
                var slotEnd = cursor.AddHours(1);
                cursor = cursor.AddHours(1);

                if (slotStart < now) continue;
                if (existingStarts.Contains(slotStart)) continue;
                if (timeOffs.Any(t => slotStart < t.EndAt && slotEnd > t.StartAt)) continue;

                newSlots.Add(new TutorAvailabilitySlot
                {
                    TutorProfileId = tutorProfileId,
                    StartTime = slotStart,
                    EndTime = slotEnd,
                    Capacity = 1,
                    IsBooked = false
                });
                existingStarts.Add(slotStart);
            }
        }

        if (newSlots.Any())
        {
            _context.TutorAvailabilitySlots.AddRange(newSlots);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<IActionResult> Schedule(string view = "week", DateTime? date = null)
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");

        await SetTutorSidebarContextAsync("schedule", tutor);
        await GenerateUpcomingSlotsAsync(tutor.Id);

        var anchor = (date ?? DateTime.Today).Date;
        var now = DateTime.Now;

        DateTime rangeStart, rangeEndExclusive;
        string rangeLabel;

        if (view == "month")
        {
            var monthStart = new DateTime(anchor.Year, anchor.Month, 1);
            int leadingDays = (int)monthStart.DayOfWeek; // Sunday = 0, matches mockup's Sun-first grid
            rangeStart = monthStart.AddDays(-leadingDays);
            rangeEndExclusive = rangeStart.AddDays(42); // fixed 6-row grid
            rangeLabel = monthStart.ToString("MMMM yyyy");
        }
        else
        {
            view = "week";
            int diffToMonday = ((int)anchor.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            rangeStart = anchor.AddDays(-diffToMonday);
            rangeEndExclusive = rangeStart.AddDays(7);
            var rangeEndDisplay = rangeStart.AddDays(6);
            rangeLabel = rangeStart.Month == rangeEndDisplay.Month
                ? $"{rangeStart.Day} - {rangeEndDisplay.Day} {rangeStart:MMMM yyyy}"
                : $"{rangeStart:d MMM} - {rangeEndDisplay:d MMM yyyy}";
        }

        var bookings = await _context.Bookings
            .Include(b => b.StudentProfile).ThenInclude(s => s.User)
            .Include(b => b.TutorAvailabilitySlot)
            .Where(b => b.TutorProfileId == tutor.Id
                && b.Status != "Cancelled"
                && b.TutorAvailabilitySlot.StartTime >= rangeStart
                && b.TutorAvailabilitySlot.StartTime < rangeEndExclusive)
            .ToListAsync();

        var timeOffsInRange = await _context.TutorTimeOffs
            .Where(t => t.TutorProfileId == tutor.Id && t.StartAt < rangeEndExclusive && t.EndAt > rangeStart)
            .OrderBy(t => t.StartAt)
            .ToListAsync();

        var rules = await _context.TutorWeeklyAvailabilityRules
            .Where(r => r.TutorProfileId == tutor.Id)
            .ToListAsync();

        var days = new List<ScheduleDayViewModel>();
        for (var d = rangeStart; d < rangeEndExclusive; d = d.AddDays(1))
        {
            var dayBookings = bookings.Where(b => b.TutorAvailabilitySlot.StartTime.Date == d.Date).ToList();
            var events = dayBookings
                .GroupBy(b => b.TutorAvailabilitySlotId)
                .Select(g => new ScheduleEventViewModel
                {
                    SlotId = g.Key,
                    StartTime = g.First().TutorAvailabilitySlot.StartTime,
                    EndTime = g.First().TutorAvailabilitySlot.EndTime,
                    Capacity = g.First().TutorAvailabilitySlot.Capacity,
                    Bookings = g.Select(b => new ScheduleBookingRowViewModel
                    {
                        BookingId = b.Id,
                        StudentName = b.StudentProfile.User.FullName,
                        Subject = b.Subject,
                        Status = b.Status
                    }).ToList()
                })
                .OrderBy(e => e.StartTime)
                .ToList();

            var dayRule = rules.FirstOrDefault(r => r.DayOfWeek == d.DayOfWeek);

            days.Add(new ScheduleDayViewModel
            {
                Date = d,
                IsCurrentPeriod = view != "month" || d.Month == anchor.Month,
                IsToday = d.Date == now.Date,
                IsDayOff = dayRule == null || dayRule.IsDayOff,
                Events = events,
                BlockedRanges = timeOffsInRange
                    .Where(t => t.StartAt.Date <= d.Date && t.EndAt.Date >= d.Date)
                    .Select(t => new TimeOffRowViewModel { Id = t.Id, StartAt = t.StartAt, EndAt = t.EndAt, Reason = t.Reason })
                    .ToList()
            });
        }

        string[] dayLabels = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
        var orderedDows = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday };
        var weeklyAvailability = orderedDows.Select((dow, i) =>
        {
            var rule = rules.FirstOrDefault(r => r.DayOfWeek == dow);
            return new WeeklyAvailabilityDayViewModel
            {
                DayOfWeek = dow,
                DayLabel = dayLabels[i],
                IsDayOff = rule?.IsDayOff ?? true,
                StartTime = rule?.StartTime,
                EndTime = rule?.EndTime
            };
        }).ToList();

        var monthStartForStats = new DateTime(anchor.Year, anchor.Month, 1);
        var monthEndForStats = monthStartForStats.AddMonths(1);
        var monthBookings = await _context.Bookings
            .Include(b => b.TutorAvailabilitySlot)
            .Where(b => b.TutorProfileId == tutor.Id
                && b.TutorAvailabilitySlot.StartTime >= monthStartForStats
                && b.TutorAvailabilitySlot.StartTime < monthEndForStats)
            .ToListAsync();

        var monthTimeOffs = await _context.TutorTimeOffs
            .Where(t => t.TutorProfileId == tutor.Id && t.StartAt < monthEndForStats && t.EndAt > monthStartForStats)
            .ToListAsync();

        var blockedDaysCount = Enumerable.Range(0, (monthEndForStats - monthStartForStats).Days)
            .Select(i => monthStartForStats.AddDays(i))
            .Count(day => monthTimeOffs.Any(t => t.StartAt.Date <= day && t.EndAt.Date >= day));

        var upcomingTimeOff = await _context.TutorTimeOffs
            .Where(t => t.TutorProfileId == tutor.Id && t.EndAt >= now)
            .OrderBy(t => t.StartAt)
            .Take(10)
            .Select(t => new TimeOffRowViewModel { Id = t.Id, StartAt = t.StartAt, EndAt = t.EndAt, Reason = t.Reason })
            .ToListAsync();

        var vm = new SchedulePageViewModel
        {
            ViewMode = view,
            AnchorDate = anchor,
            RangeStart = rangeStart,
            RangeLabel = rangeLabel,
            Days = days,
            WeeklyAvailability = weeklyAvailability,
            UpcomingTimeOff = upcomingTimeOff,
            SessionsScheduledCount = monthBookings.Count(b => b.Status != "Cancelled"),
            PendingRequestsCount = monthBookings.Count(b => b.Status == "Pending"),
            BlockedDaysCount = blockedDaysCount,
            MissedSessionsCount = monthBookings.Count(b => b.Status == "Missed")
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateWeeklyAvailability(List<string> dayOfWeek, List<string> isDayOff, List<string> startTime, List<string> endTime)
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");

        var existingRules = await _context.TutorWeeklyAvailabilityRules
            .Where(r => r.TutorProfileId == tutor.Id)
            .ToListAsync();

        for (int i = 0; i < dayOfWeek.Count; i++)
        {
            if (!Enum.TryParse<DayOfWeek>(dayOfWeek[i], out var dow)) continue;

            bool dayOff = i >= isDayOff.Count || string.Equals(isDayOff[i], "true", StringComparison.OrdinalIgnoreCase);
            TimeSpan? start = null, end = null;

            if (!dayOff)
            {
                TimeSpan? parsedStart = i < startTime.Count && TimeSpan.TryParse(startTime[i], out var s) ? s : null;
                TimeSpan? parsedEnd = i < endTime.Count && TimeSpan.TryParse(endTime[i], out var e) ? e : null;

                if (parsedStart == null || parsedEnd == null || parsedStart >= parsedEnd)
                {
                    dayOff = true;
                }
                else
                {
                    start = parsedStart;
                    end = parsedEnd;
                }
            }

            var rule = existingRules.FirstOrDefault(r => r.DayOfWeek == dow);
            if (rule == null)
            {
                rule = new TutorWeeklyAvailabilityRule { TutorProfileId = tutor.Id, DayOfWeek = dow };
                _context.TutorWeeklyAvailabilityRules.Add(rule);
            }
            rule.IsDayOff = dayOff;
            rule.StartTime = start;
            rule.EndTime = end;
        }

        await _context.SaveChangesAsync();
        await GenerateUpcomingSlotsAsync(tutor.Id);

        TempData["ScheduleSuccess"] = "Weekly availability updated.";
        return RedirectToAction("Schedule");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTimeOff(DateTime startAt, DateTime endAt, string? reason)
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");

        if (startAt >= endAt)
        {
            TempData["ScheduleError"] = "End time must be after start time.";
            return RedirectToAction("Schedule");
        }

        var hasConflict = await _context.Bookings
            .Include(b => b.TutorAvailabilitySlot)
            .AnyAsync(b => b.TutorProfileId == tutor.Id
                && (b.Status == "Pending" || b.Status == "Confirmed")
                && b.TutorAvailabilitySlot.StartTime < endAt
                && b.TutorAvailabilitySlot.EndTime > startAt);

        if (hasConflict)
        {
            TempData["ScheduleError"] = "You have active sessions in that period - handle those first before blocking this time.";
            return RedirectToAction("Schedule");
        }

        _context.TutorTimeOffs.Add(new TutorTimeOff
        {
            TutorProfileId = tutor.Id,
            StartAt = startAt,
            EndAt = endAt,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            CreatedAt = DateTime.Now
        });

        // Only remove slots with NO booking history at all (even a cancelled
        // booking still references its slot via a Restrict FK, so it can't be
        // deleted - only genuinely untouched slots are safe to remove here).
        var removableSlotIds = await _context.TutorAvailabilitySlots
            .Where(s => s.TutorProfileId == tutor.Id && s.StartTime < endAt && s.EndTime > startAt)
            .Where(s => !_context.Bookings.Any(b => b.TutorAvailabilitySlotId == s.Id))
            .Select(s => s.Id)
            .ToListAsync();

        if (removableSlotIds.Any())
        {
            var removableSlots = await _context.TutorAvailabilitySlots.Where(s => removableSlotIds.Contains(s.Id)).ToListAsync();
            _context.TutorAvailabilitySlots.RemoveRange(removableSlots);
        }

        await _context.SaveChangesAsync();

        TempData["ScheduleSuccess"] = "Time off blocked.";
        return RedirectToAction("Schedule");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveTimeOff(int id)
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");

        var timeOff = await _context.TutorTimeOffs
            .FirstOrDefaultAsync(t => t.Id == id && t.TutorProfileId == tutor.Id);

        if (timeOff != null)
        {
            _context.TutorTimeOffs.Remove(timeOff);
            await _context.SaveChangesAsync();
            await GenerateUpcomingSlotsAsync(tutor.Id);
        }

        return RedirectToAction("Schedule");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetSlotCapacity(int slotId, int capacity)
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");

        capacity = Math.Clamp(capacity, 1, 8);

        var slot = await _context.TutorAvailabilitySlots
            .FirstOrDefaultAsync(s => s.Id == slotId && s.TutorProfileId == tutor.Id);

        if (slot != null)
        {
            var activeCount = await _context.Bookings
                .CountAsync(b => b.TutorAvailabilitySlotId == slotId && b.Status != "Cancelled");

            if (capacity >= activeCount)
            {
                slot.Capacity = capacity;
                slot.IsBooked = activeCount >= capacity;
                await _context.SaveChangesAsync();
                TempData["ScheduleSuccess"] = "Slot capacity updated.";
            }
            else
            {
                TempData["ScheduleError"] = $"Can't set capacity below {activeCount} - that many students are already booked.";
            }
        }

        return RedirectToAction("Schedule");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkSessionMissed(int id)
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");

        var booking = await _context.Bookings
            .Include(b => b.TutorAvailabilitySlot)
            .FirstOrDefaultAsync(b => b.Id == id && b.TutorProfileId == tutor.Id && b.Status == "Confirmed");

        if (booking != null && booking.TutorAvailabilitySlot.StartTime <= DateTime.Now)
        {
            booking.Status = "Missed";
            await _context.SaveChangesAsync();
        }

        return RedirectToAction("Schedule");
    }
}