using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using TutorBridgeNepal.Data;
using TutorBridgeNepal.Helpers;
using TutorBridgeNepal.Models;
using TutorBridgeNepal.ViewModels;

namespace TutorBridgeNepal.Controllers;

[Authorize(Roles = "Tutor")]
public class TutorController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IWebHostEnvironment _webHostEnvironment;

    public TutorController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, IWebHostEnvironment webHostEnvironment)
    {
        _context = context;
        _userManager = userManager;
        _signInManager = signInManager;
        _webHostEnvironment = webHostEnvironment;
    }

    private static readonly string[] AllowedWhileUnverified =
    {
        "VerificationPending",
        "UploadVerificationDocument",
        "RemoveVerificationDocument",
        "DownloadOwnVerificationDocument"
    };

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var actionName = context.ActionDescriptor.RouteValues?["action"];

        if (actionName == null || !AllowedWhileUnverified.Contains(actionName))
        {
            var tutor = await GetCurrentTutorProfileAsync();
            if (tutor != null && !tutor.IsVerified)
            {
                context.Result = RedirectToAction("VerificationPending");
                return;
            }
        }

        await next();
    }

    private static string GetInitials(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "?";
        if (parts.Length == 1) return parts[0][..Math.Min(2, parts[0].Length)].ToUpper();
        return $"{parts[0][0]}{parts[^1][0]}".ToUpper();
    }

    private static readonly Dictionary<string, string> DistrictToProvince = new()
    {
        ["Kathmandu"] = "Bagmati Province",
        ["Lalitpur"] = "Bagmati Province",
        ["Bhaktapur"] = "Bagmati Province",
        ["Chitwan"] = "Bagmati Province",
        ["Pokhara"] = "Gandaki Province",
        ["Biratnagar"] = "Koshi Province",
    };

    private static readonly (string Type, string Label, string Icon)[] RequiredVerificationDocuments =
    {
        ("Citizenship",       "Citizenship",       "🪪"),
        ("CVResume",          "CV / Resume",       "📄"),
        ("DegreeCertificate", "Degree Certificate","🎓"),
        ("PoliceReport",      "Police Report",     "🛡️"),
    };

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
        ViewData["ShowAvailabilityBadge"] = tutor.ShowAvailabilityBadge;
    }

    // ── VerificationPending ───────────────────────────────────────────────

    public async Task<IActionResult> VerificationPending()
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");

        if (tutor.IsVerified)
            return RedirectToAction("Dashboard");

        var credentials = await _context.TutorCredentials
            .Where(c => c.TutorProfileId == tutor.Id)
            .ToListAsync();

        var vm = new TutorVerificationPendingViewModel
        {
            FullName = tutor.User.FullName,
            Initials = GetInitials(tutor.User.FullName),
            IsRejected = tutor.VerificationRejected,
            SubmittedAt = tutor.User.CreatedAt,
            AdminNote = tutor.VerificationNote,
            RequiredDocuments = RequiredVerificationDocuments.Select(rd =>
            {
                var match = credentials.FirstOrDefault(c => c.DocumentType == rd.Type);
                return new TutorDocumentSlotViewModel
                {
                    DocumentType = rd.Type,
                    Label = rd.Label,
                    Icon = rd.Icon,
                    IsUploaded = match != null,
                    CredentialId = match?.Id,
                    OriginalFileName = match?.FileName,
                    UploadedAt = match?.UploadedAt
                };
            }).ToList()
        };

        return View(vm);
    }

    // ── Dashboard ─────────────────────────────────────────────────────────

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
            StudentProfileId = b.StudentProfileId,
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
                .Where(b => b.Status != "Pending" && b.TutorAvailabilitySlot.StartTime < now)
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
    public async Task<IActionResult> AcceptBooking(int id, string returnTo = "Dashboard")
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");

        var booking = await _context.Bookings
            .FirstOrDefaultAsync(b => b.Id == id && b.TutorProfileId == tutor.Id && b.Status == "Pending");

        if (booking != null)
        {
            booking.Status = "Confirmed";
            booking.DecidedAt = DateTime.Now;
            await _context.SaveChangesAsync();
        }

        return returnTo switch
        {
            "SessionRequests" => RedirectToAction("SessionRequests"),
            "MyStudents" => RedirectToAction("MyStudents"),
            _ => RedirectToAction("Dashboard")
        };
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeclineBooking(int id, string returnTo = "Dashboard")
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");

        var booking = await _context.Bookings
            .Include(b => b.TutorAvailabilitySlot)
            .FirstOrDefaultAsync(b => b.Id == id && b.TutorProfileId == tutor.Id && b.Status == "Pending");

        if (booking != null)
        {
            booking.Status = "Cancelled";
            booking.DeclinedByTutor = true;
            booking.DecidedAt = DateTime.Now;

            var remainingActive = await _context.Bookings.CountAsync(b =>
                b.TutorAvailabilitySlotId == booking.TutorAvailabilitySlotId
                && b.Id != booking.Id && b.Status != "Cancelled");
            booking.TutorAvailabilitySlot.IsBooked = remainingActive >= booking.TutorAvailabilitySlot.Capacity;

            await _context.SaveChangesAsync();
        }

        return returnTo == "SessionRequests" ? RedirectToAction("SessionRequests") : RedirectToAction("Dashboard");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkSessionCompleted(int id)
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");

        var booking = await _context.Bookings
            .Include(b => b.TutorAvailabilitySlot)
            .Include(b => b.StudentProfile).ThenInclude(s => s.User)
            .FirstOrDefaultAsync(b => b.Id == id && b.TutorProfileId == tutor.Id && b.Status == "Confirmed");

        // Can only mark a session complete once it has actually started - stops
        // a tutor from marking a future session done before it happens.
        if (booking != null && booking.TutorAvailabilitySlot.StartTime <= DateTime.Now)
        {
            booking.Status = "Completed";

            NotificationHelper.Create(_context,
                type: "System",
                title: "Session completed successfully",
                message: $"{tutor.User.FullName} completed {booking.Subject} session with {booking.StudentProfile.User.FullName}",
                icon: "✅",
                actionLabel: "View details",
                actionUrl: Url.Action("SessionLogs", "Admin", new { search = $"SES-{booking.Id:D4}" }));

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

    public async Task<IActionResult> SessionRequests(string tab = "pending", string sort = "newest")
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");

        await SetTutorSidebarContextAsync("sessionrequests", tutor);

        var now = DateTime.Now;
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var monthEnd = monthStart.AddMonths(1);

        var allBookings = await _context.Bookings
            .Include(b => b.StudentProfile).ThenInclude(s => s.User)
            .Include(b => b.TutorAvailabilitySlot)
            .Where(b => b.TutorProfileId == tutor.Id)
            .OrderBy(b => b.CreatedAt)
            .ToListAsync();

        var reviewsGivenToThisTutor = await _context.Reviews
            .Where(r => r.TutorProfileId == tutor.Id)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        SessionRequestRowViewModel BuildRow(Booking b)
        {
            var priorBookings = allBookings
                .Where(x => x.StudentProfileId == b.StudentProfileId && x.CreatedAt < b.CreatedAt)
                .ToList();

            var lastCompleted = priorBookings
                .Where(x => x.Status == "Completed")
                .OrderByDescending(x => x.TutorAvailabilitySlot.StartTime)
                .FirstOrDefault();

            var lastReview = reviewsGivenToThisTutor
                .FirstOrDefault(r => r.StudentProfileId == b.StudentProfileId);

            return new SessionRequestRowViewModel
            {
                BookingId = b.Id,
                StudentName = b.StudentProfile.User.FullName,
                StudentInitials = GetInitials(b.StudentProfile.User.FullName),
                GradeLevel = b.StudentProfile.GradeLevel,
                District = b.StudentProfile.User.District,
                IsReturningStudent = priorBookings.Any(),
                PriorSessionsCount = priorBookings.Count(x => x.Status != "Cancelled"),
                LastSessionAt = lastCompleted?.TutorAvailabilitySlot.StartTime,
                LastRatingGiven = lastReview?.Rating,
                Subject = b.Subject,
                StartTime = b.TutorAvailabilitySlot.StartTime,
                EndTime = b.TutorAvailabilitySlot.EndTime,
                Note = b.Note,
                CreatedAt = b.CreatedAt,
                Status = b.Status,
                DeclinedByTutor = b.DeclinedByTutor
            };
        }

        IEnumerable<Booking> scoped = tab switch
        {
            "accepted" => allBookings.Where(b => b.DecidedAt != null && !b.DeclinedByTutor),
            "declined" => allBookings.Where(b => b.DeclinedByTutor),
            "all" => allBookings,
            _ => allBookings.Where(b => b.Status == "Pending")
        };

        scoped = sort == "oldest" ? scoped.OrderBy(b => b.CreatedAt) : scoped.OrderByDescending(b => b.CreatedAt);

        var decidedThisMonth = allBookings
            .Where(b => b.DecidedAt.HasValue && b.DecidedAt.Value >= monthStart && b.DecidedAt.Value < monthEnd)
            .ToList();

        var createdThisMonth = allBookings
            .Where(b => b.CreatedAt >= monthStart && b.CreatedAt < monthEnd)
            .ToList();

        var decidedAndCreatedThisMonth = createdThisMonth.Count(b => b.DecidedAt.HasValue);
        var responseRate = createdThisMonth.Any()
            ? (int)Math.Round(decidedAndCreatedThisMonth * 100.0 / createdThisMonth.Count)
            : (int?)null;

        var avgResponseMinutes = decidedThisMonth.Any()
            ? decidedThisMonth.Average(b => (b.DecidedAt!.Value - b.CreatedAt).TotalMinutes)
            : (double?)null;

        var recentlyAccepted = allBookings
            .Where(b => b.DecidedAt != null && !b.DeclinedByTutor)
            .OrderByDescending(b => b.DecidedAt)
            .Take(10)
            .Select(b => new RecentlyAcceptedRowViewModel
            {
                StudentName = b.StudentProfile.User.FullName,
                Subject = b.Subject,
                SessionDate = b.TutorAvailabilitySlot.StartTime,
                AcceptedOn = b.DecidedAt!.Value,
                ResponseTimeMinutes = (b.DecidedAt.Value - b.CreatedAt).TotalMinutes
            })
            .ToList();

        var vm = new SessionRequestsPageViewModel
        {
            Tab = tab,
            Sort = sort,
            PendingCount = allBookings.Count(b => b.Status == "Pending"),
            AcceptedThisMonthCount = decidedThisMonth.Count(b => !b.DeclinedByTutor),
            DeclinedThisMonthCount = decidedThisMonth.Count(b => b.DeclinedByTutor),
            AvgResponseTimeMinutes = avgResponseMinutes,
            ResponseRatePercent = responseRate,
            Requests = scoped.Select(BuildRow).ToList(),
            RecentlyAccepted = recentlyAccepted
        };

        return View(vm);
    }

    private async Task GenerateUpcomingSlotsAsync(int tutorProfileId, int daysAhead = 30)
    {
        var rules = await _context.TutorWeeklyAvailabilityRules
            .Where(r => r.TutorProfileId == tutorProfileId)
            .ToListAsync();

        if (!rules.Any()) return; // tutor hasn't set up weekly availability yet

        var tutorTeachingMode = await _context.TutorProfiles
            .Where(t => t.Id == tutorProfileId)
            .Select(t => t.TeachingMode)
            .FirstOrDefaultAsync();

        // "Online & In-person" tutors alternate by day so their real slot mix
        // isn't 100% one or the other - a simple, deterministic rule rather
        // than random, so regenerating slots stays consistent.
        string ModeForDate(DateTime date) => tutorTeachingMode switch
        {
            "In-person only" => "In-person",
            "Online only" => "Online",
            _ => date.DayOfYear % 2 == 0 ? "Online" : "In-person"
        };

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
                    IsBooked = false,
                    Mode = ModeForDate(date)
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
            int leadingDays = (int)monthStart.DayOfWeek;
            rangeStart = monthStart.AddDays(-leadingDays);
            rangeEndExclusive = rangeStart.AddDays(42);
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
                    dayOff = true;
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

    public async Task<IActionResult> MyStudents(string tab = "active", string? search = null, string? grade = null, string? subject = null, string sort = "recent")
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");

        await SetTutorSidebarContextAsync("mystudents", tutor);

        var now = DateTime.Now;

        var allBookings = await _context.Bookings
            .Include(b => b.StudentProfile).ThenInclude(s => s.User)
            .Include(b => b.TutorAvailabilitySlot)
            .Where(b => b.TutorProfileId == tutor.Id)
            .ToListAsync();

        var reviewsGivenToTutor = await _context.Reviews
            .Where(r => r.TutorProfileId == tutor.Id)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        var studentGroups = allBookings
            .GroupBy(b => b.StudentProfileId)
            .ToList();

        TutorStudentCardViewModel BuildCard(IGrouping<int, Booking> g)
        {
            var studentBookings = g.ToList();
            var nonCancelled = studentBookings.Where(b => b.Status != "Cancelled").ToList();
            var student = studentBookings.First().StudentProfile;

            var completed = nonCancelled.Where(b => b.Status == "Completed").ToList();
            var lastSession = completed.OrderByDescending(b => b.TutorAvailabilitySlot.StartTime).FirstOrDefault();
            var nextConfirmed = nonCancelled
                .Where(b => b.Status == "Confirmed" && b.TutorAvailabilitySlot.StartTime >= now)
                .OrderBy(b => b.TutorAvailabilitySlot.StartTime)
                .FirstOrDefault();
            var pending = nonCancelled.Where(b => b.Status == "Pending").OrderBy(b => b.CreatedAt).FirstOrDefault();
            var lastReview = reviewsGivenToTutor.FirstOrDefault(r => r.StudentProfileId == g.Key);

            var hasFutureOrPending = nextConfirmed != null || pending != null;

            return new TutorStudentCardViewModel
            {
                StudentProfileId = student.Id,
                FullName = student.User.FullName,
                Initials = GetInitials(student.User.FullName),
                GradeLevel = student.GradeLevel,
                District = student.User.District,
                Subjects = nonCancelled.Select(b => b.Subject).Distinct().OrderBy(s => s).ToList(),
                SessionsCompleted = completed.Count,
                LastSessionAt = lastSession?.TutorAvailabilitySlot.StartTime,
                NextSessionAt = nextConfirmed?.TutorAvailabilitySlot.StartTime,
                RatingGiven = lastReview?.Rating,
                IsNew = completed.Count == 0,
                HasPendingRequest = pending != null,
                PendingBookingId = pending?.Id,
                PendingRequestedFor = pending?.TutorAvailabilitySlot.StartTime,
                StatusLabel = hasFutureOrPending ? "Active" : (completed.Count > 0 ? "Past" : "Active")
            };
        }

        var allCards = studentGroups.Select(BuildCard).ToList();
        var activeCards = allCards.Where(c => c.StatusLabel == "Active").ToList();
        var pastCards = allCards.Where(c => c.StatusLabel == "Past").ToList();

        IEnumerable<TutorStudentCardViewModel> scoped = tab switch
        {
            "past" => pastCards,
            "all" => allCards,
            _ => activeCards
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            scoped = scoped.Where(c => c.FullName.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(grade))
            scoped = scoped.Where(c => c.GradeLevel == grade);

        if (!string.IsNullOrWhiteSpace(subject))
            scoped = scoped.Where(c => c.Subjects.Contains(subject));

        scoped = sort switch
        {
            "name" => scoped.OrderBy(c => c.FullName),
            "sessions" => scoped.OrderByDescending(c => c.SessionsCompleted),
            _ => scoped.OrderByDescending(c => c.NextSessionAt ?? c.LastSessionAt ?? DateTime.MinValue)
        };

        var totalSessions = allBookings.Count(b => b.Status != "Cancelled");
        var avgSessionsPerStudent = allCards.Any() ? Math.Round((double)totalSessions / allCards.Count, 1) : 0;

        var vm = new MyStudentsPageViewModel
        {
            Tab = tab,
            Search = search,
            Grade = grade,
            Subject = subject,
            Sort = sort,
            ActiveCount = activeCards.Count,
            PastCount = pastCards.Count,
            AllCount = allCards.Count,
            TotalSessions = totalSessions,
            AvgSessionsPerStudent = avgSessionsPerStudent,
            AvgRatingGivenToTutor = tutor.AverageRating,
            RatingCount = tutor.ReviewCount,
            GradeOptions = allCards.Where(c => !string.IsNullOrWhiteSpace(c.GradeLevel)).Select(c => c.GradeLevel!).Distinct().OrderBy(x => x).ToList(),
            SubjectOptions = allCards.SelectMany(c => c.Subjects).Distinct().OrderBy(x => x).ToList(),
            Students = scoped.ToList()
        };

        return View(vm);
    }


    // Reached from the "View profile" button on a Messages thread (and from
    // My Students). Only shows a student the tutor actually has booking
    // history with - a tutor can't view an arbitrary student's profile by
    // guessing an id.
    public async Task<IActionResult> StudentDetail(int id)
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");

        await SetTutorSidebarContextAsync("messages", tutor);

        var student = await _context.StudentProfiles.Include(s => s.User).FirstOrDefaultAsync(s => s.Id == id);
        if (student == null) return NotFound();

        var bookings = await _context.Bookings
            .Include(b => b.TutorAvailabilitySlot)
            .Where(b => b.TutorProfileId == tutor.Id && b.StudentProfileId == id)
            .OrderByDescending(b => b.TutorAvailabilitySlot.StartTime)
            .ToListAsync();

        if (!bookings.Any())
            return NotFound();

        var nonCancelled = bookings.Where(b => b.Status != "Cancelled").ToList();
        var completed = nonCancelled.Where(b => b.Status == "Completed").ToList();

        var vm = new TutorStudentDetailViewModel
        {
            StudentProfileId = student.Id,
            FullName = student.User.FullName,
            Initials = GetInitials(student.User.FullName),
            GradeLevel = student.GradeLevel,
            District = student.User.District,
            SchoolName = student.SchoolName,
            CurriculumBoard = student.CurriculumBoard,
            LearningGoal = student.LearningGoal,
            TotalSessions = nonCancelled.Count,
            CompletedSessions = completed.Count,
            RecentSessions = nonCancelled.Take(15).Select(b => new StudentSessionHistoryRow
            {
                Subject = b.Subject,
                Date = b.TutorAvailabilitySlot.StartTime,
                Status = b.Status
            }).ToList()
        };

        return View(vm);
    }
    public async Task<IActionResult> Messages(int? studentProfileId, string tab = "all", string? search = null)
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");

        await SetTutorSidebarContextAsync("messages", tutor);

        var now = DateTime.Now;

        var allBookings = await _context.Bookings
            .Include(b => b.StudentProfile).ThenInclude(s => s.User)
            .Include(b => b.TutorAvailabilitySlot)
            .Where(b => b.TutorProfileId == tutor.Id)
            .ToListAsync();

        var studentIds = allBookings.Select(b => b.StudentProfileId).Distinct().ToList();

        var allMessages = await _context.Messages
            .Where(m => m.TutorProfileId == tutor.Id && studentIds.Contains(m.StudentProfileId))
            .OrderByDescending(m => m.SentAt)
            .ToListAsync();

        var conversations = studentIds.Select(sid =>
        {
            var studentBookings = allBookings.Where(b => b.StudentProfileId == sid).ToList();
            var student = studentBookings.First().StudentProfile;
            var threadMessages = allMessages.Where(m => m.StudentProfileId == sid).ToList();
            var last = threadMessages.FirstOrDefault();

            var nonCancelled = studentBookings.Where(b => b.Status != "Cancelled").ToList();
            var completedCount = nonCancelled.Count(b => b.Status == "Completed");
            var nextSession = nonCancelled
                .Where(b => b.Status == "Confirmed" && b.TutorAvailabilitySlot.StartTime >= now)
                .OrderBy(b => b.TutorAvailabilitySlot.StartTime)
                .FirstOrDefault();
            var lastSession = nonCancelled
                .Where(b => b.Status == "Completed")
                .OrderByDescending(b => b.TutorAvailabilitySlot.StartTime)
                .FirstOrDefault();

            return new TutorConversationListItemViewModel
            {
                StudentProfileId = sid,
                StudentName = student.User.FullName,
                StudentInitials = GetInitials(student.User.FullName),
                LastMessagePreview = last?.Content,
                LastMessageAt = last?.SentAt,
                UnreadCount = threadMessages.Count(m => m.SenderRole == "Student" && !m.IsRead),
                Subjects = nonCancelled.Select(b => b.Subject).Distinct().OrderBy(s => s).ToList(),
                IsNew = completedCount == 0,
                NextSessionAt = nextSession?.TutorAvailabilitySlot.StartTime,
                LastSessionAt = lastSession?.TutorAvailabilitySlot.StartTime
            };
        })
        .OrderByDescending(c => c.LastMessageAt ?? DateTime.MinValue)
        .ToList();

        var vm = new TutorMessagesPageViewModel { Tab = tab, Search = search };

        var activeStudentId = studentProfileId ?? conversations.FirstOrDefault()?.StudentProfileId;

        if (activeStudentId.HasValue)
        {
            var activeConvo = conversations.FirstOrDefault(c => c.StudentProfileId == activeStudentId.Value);
            if (activeConvo != null)
            {
                var threadMessages = allMessages
                    .Where(m => m.StudentProfileId == activeStudentId.Value)
                    .OrderBy(m => m.SentAt)
                    .ToList();

                var unreadFromStudent = threadMessages.Where(m => m.SenderRole == "Student" && !m.IsRead).ToList();
                if (unreadFromStudent.Any())
                {
                    var idsToMark = unreadFromStudent.Select(m => m.Id).ToList();
                    var toUpdate = await _context.Messages.Where(m => idsToMark.Contains(m.Id)).ToListAsync();
                    foreach (var m in toUpdate) m.IsRead = true;
                    await _context.SaveChangesAsync();
                }

                activeConvo.UnreadCount = 0;

                vm.ActiveStudentProfileId = activeConvo.StudentProfileId;
                vm.ActiveStudentName = activeConvo.StudentName;
                vm.ActiveStudentInitials = activeConvo.StudentInitials;
                vm.ActiveStudentIsNew = activeConvo.IsNew;
                vm.ActiveStudentIsActive = activeConvo.NextSessionAt.HasValue;
                vm.ActiveStudentSubjects = activeConvo.Subjects;

                var studentProfile = allBookings.First(b => b.StudentProfileId == activeStudentId.Value).StudentProfile;
                vm.ActiveStudentGradeLevel = studentProfile.GradeLevel;

                vm.Messages = threadMessages.Select(m => new TutorMessageBubbleViewModel
                {
                    Id = m.Id,
                    SenderRole = m.SenderRole,
                    Content = m.Content,
                    SentAt = m.SentAt,
                    IsRead = m.IsRead
                }).ToList();
            }
        }

        IEnumerable<TutorConversationListItemViewModel> scopedConvos = tab switch
        {
            "unread" => conversations.Where(c => c.UnreadCount > 0),
            _ => conversations
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            scopedConvos = scopedConvos.Where(c => c.StudentName.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        vm.Conversations = scopedConvos.ToList();
        vm.TotalUnread = conversations.Sum(c => c.UnreadCount);

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendMessage(int studentProfileId, string content)
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");

        if (string.IsNullOrWhiteSpace(content))
            return RedirectToAction("Messages", new { studentProfileId });

        var hasRelationship = await _context.Bookings
            .AnyAsync(b => b.TutorProfileId == tutor.Id && b.StudentProfileId == studentProfileId);

        if (!hasRelationship) return RedirectToAction("Messages");

        _context.Messages.Add(new Message
        {
            StudentProfileId = studentProfileId,
            TutorProfileId = tutor.Id,
            SenderRole = "Tutor",
            Content = content.Trim(),
            SentAt = DateTime.Now,
            IsRead = false
        });

        await _context.SaveChangesAsync();

        return RedirectToAction("Messages", new { studentProfileId });
    }

    public async Task<IActionResult> Reviews(string tab = "all", string sort = "newest")
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");

        await SetTutorSidebarContextAsync("reviews", tutor);

        var reviews = await _context.Reviews
            .Include(r => r.StudentProfile).ThenInclude(s => s.User)
            .Include(r => r.Booking).ThenInclude(b => b.TutorAvailabilitySlot)
            .Where(r => r.TutorProfileId == tutor.Id)
            .ToListAsync();

        var starDistribution = Enumerable.Range(1, 5)
            .Select(star => new StarBucketViewModel
            {
                Stars = star,
                Count = reviews.Count(r => r.Rating == star),
                Percent = reviews.Any() ? (int)Math.Round(reviews.Count(r => r.Rating == star) * 100.0 / reviews.Count) : 0
            })
            .OrderByDescending(s => s.Stars)
            .ToList();

        IEnumerable<Review> scoped = tab switch
        {
            "5star" => reviews.Where(r => r.Rating == 5),
            "4star" => reviews.Where(r => r.Rating == 4),
            "3below" => reviews.Where(r => r.Rating <= 3),
            _ => reviews
        };

        scoped = sort == "oldest" ? scoped.OrderBy(r => r.CreatedAt) : scoped.OrderByDescending(r => r.CreatedAt);

        var phraseGroups = new (string Label, string[] Matches)[]
        {
            ("Clear explanations", new[] { "clear" }),
            ("Patient",            new[] { "patient" }),
            ("Well-prepared",      new[] { "prepared" }),
            ("Improved grades",    new[] { "improved", "grade" }),
            ("Punctual",           new[] { "punctual", "on time" }),
            ("Exam-focused",       new[] { "exam", "see prep", "board exam" }),
            ("Helpful",            new[] { "helpful" }),
            ("Friendly",           new[] { "friendly" }),
            ("Recommend",          new[] { "recommend" }),
            ("Runs long",          new[] { "long", "runs over" }),
        };

        var allComments = reviews.Where(r => !string.IsNullOrWhiteSpace(r.Comment)).Select(r => r.Comment!.ToLowerInvariant()).ToList();
        var commonPhrases = phraseGroups
            .Select(p => new { p.Label, Count = allComments.Count(c => p.Matches.Any(m => c.Contains(m))) })
            .Where(p => p.Count > 0)
            .OrderByDescending(p => p.Count)
            .Take(8)
            .Select(p => p.Label)
            .ToList();

        var platformRatings = await _context.TutorProfiles
            .Where(t => t.ReviewCount > 0)
            .Select(t => t.AverageRating)
            .ToListAsync();
        var platformAverage = platformRatings.Any() ? Math.Round(platformRatings.Average(), 2) : 0m;

        var allBookings = await _context.Bookings.Where(b => b.TutorProfileId == tutor.Id).ToListAsync();
        var decidedCount = allBookings.Count(b => b.DecidedAt.HasValue);
        var responseRate = allBookings.Any() ? (int?)Math.Round(decidedCount * 100.0 / allBookings.Count) : null;

        var studentBookingCounts = allBookings
            .Where(b => b.Status != "Cancelled")
            .GroupBy(b => b.StudentProfileId)
            .Select(g => g.Count())
            .ToList();
        var repeatRate = studentBookingCounts.Any()
            ? (int?)Math.Round(studentBookingCounts.Count(c => c >= 2) * 100.0 / studentBookingCounts.Count)
            : null;

        var isTopFivePercent = false;
        if (platformRatings.Count >= 2 && tutor.ReviewCount > 0)
        {
            var rank = platformRatings.Count(r => r > tutor.AverageRating) + 1;
            var percentile = rank * 100.0 / platformRatings.Count;
            isTopFivePercent = percentile <= 5;
        }

        var vm = new TutorReviewsPageViewModel
        {
            Tab = tab,
            Sort = sort,
            AverageRating = tutor.AverageRating,
            ReviewCount = tutor.ReviewCount,
            StarDistribution = starDistribution,
            FiveStarCount = reviews.Count(r => r.Rating == 5),
            FourStarCount = reviews.Count(r => r.Rating == 4),
            ThreeAndBelowCount = reviews.Count(r => r.Rating <= 3),
            CommonPhrases = commonPhrases,
            PlatformAverageRating = platformAverage,
            ResponseRatePercent = responseRate,
            RepeatBookingRatePercent = repeatRate,
            IsTopFivePercent = isTopFivePercent,
            Reviews = scoped.Select(r => new TutorReviewRowViewModel2
            {
                ReviewId = r.Id,
                StudentName = r.StudentProfile.User.FullName,
                StudentInitials = GetInitials(r.StudentProfile.User.FullName),
                Rating = r.Rating,
                Comment = r.Comment,
                Subject = r.Booking.Subject,
                SessionDate = r.Booking.TutorAvailabilitySlot.StartTime,
                CreatedAt = r.CreatedAt,
                TutorReply = r.TutorReply,
                TutorRepliedAt = r.TutorRepliedAt
            }).ToList()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReplyToReview(int reviewId, string reply)
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");

        if (string.IsNullOrWhiteSpace(reply))
            return RedirectToAction("Reviews");

        var review = await _context.Reviews
            .FirstOrDefaultAsync(r => r.Id == reviewId && r.TutorProfileId == tutor.Id);

        if (review != null)
        {
            review.TutorReply = reply.Trim();
            review.TutorRepliedAt = DateTime.Now;
            await _context.SaveChangesAsync();
        }

        return RedirectToAction("Reviews");
    }

    public async Task<IActionResult> Profile()
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");

        await SetTutorSidebarContextAsync("myprofile", tutor);

        var sessionsCompleted = await _context.Bookings
            .CountAsync(b => b.TutorProfileId == tutor.Id && b.Status == "Completed");

        var subjects = await _context.TutorSubjects
            .Where(s => s.TutorProfileId == tutor.Id)
            .OrderBy(s => s.SortOrder)
            .ToListAsync();

        var credentials = await _context.TutorCredentials
            .Where(c => c.TutorProfileId == tutor.Id)
            .OrderBy(c => c.SortOrder)
            .ToListAsync();

        var platformRatings = await _context.TutorProfiles
            .Where(t => t.ReviewCount > 0)
            .Select(t => t.AverageRating)
            .ToListAsync();
        var isTopTutor = false;
        if (platformRatings.Count >= 2 && tutor.ReviewCount > 0)
        {
            var rank = platformRatings.Count(r => r > tutor.AverageRating) + 1;
            var percentile = rank * 100.0 / platformRatings.Count;
            isTopTutor = percentile <= 5;
        }

        var languages = (tutor.Languages ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var teachingStyleTags = (tutor.TeachingStyle ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        var (completionPercent, completionHint) = CalculateProfileCompletion(tutor, subjects.Count, credentials.Count, languages.Count, teachingStyleTags.Count);

        var vm = new TutorProfilePageViewModel
        {
            FullName = tutor.User.FullName,
            DisplayName = tutor.DisplayName,
            Initials = GetInitials(tutor.User.FullName),
            IsVerified = tutor.IsVerified,
            IsTopTutor = isTopTutor,
            TopTutorYear = DateTime.Now.Year,
            SubjectTags = tutor.Subjects.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            District = tutor.User.District,
            Province = tutor.User.District != null && DistrictToProvince.TryGetValue(tutor.User.District, out var prov) ? prov : null,
            YearsOfExperience = tutor.YearsOfExperience,
            SessionsCompleted = sessionsCompleted,
            AverageRating = tutor.AverageRating,
            ReviewCount = tutor.ReviewCount,
            Bio = tutor.Bio,
            TeachingMode = tutor.TeachingMode,
            Languages = languages,
            TeachingStyleTags = teachingStyleTags,
            Subjects = subjects.Select(s => new TutorSubjectRowViewModel
            {
                Id = s.Id,
                Subject = s.Subject,
                Description = s.Description
            }).ToList(),
            Credentials = credentials.Select(c => new TutorCredentialRowViewModel
            {
                Id = c.Id,
                Title = c.Title,
                FileName = c.FileName,
                Icon = c.Icon
            }).ToList(),
            ProfileCompletionPercent = completionPercent,
            ProfileCompletionHint = completionHint
        };

        return View(vm);
    }

    private static (int Percent, string? Hint) CalculateProfileCompletion(TutorProfile tutor, int subjectCount, int credentialCount, int languageCount, int teachingStyleCount)
    {
        var checklist = new (bool Done, string Label)[]
        {
            (!string.IsNullOrWhiteSpace(tutor.DisplayName),                          "Add a display name"),
            (!string.IsNullOrWhiteSpace(tutor.Bio) && tutor.Bio.Trim().Length >= 20, "Write a bio"),
            (!string.IsNullOrWhiteSpace(tutor.User.District),                        "Add your district"),
            (tutor.YearsOfExperience > 0,                                            "Add your years of experience"),
            (!string.IsNullOrWhiteSpace(tutor.TeachingMode),                         "Set your teaching mode"),
            (languageCount > 0,                                                       "Add a language you teach in"),
            (teachingStyleCount > 0,                                                  "Add a teaching style tag"),
            (subjectCount > 0,                                                        "Add a subject"),
            (credentialCount > 0,                                                     "Add a credential or document"),
            (false,                                                                   "Add a video intro"),
        };

        var doneCount = checklist.Count(c => c.Done);
        var percent = (int)Math.Round(doneCount * 100.0 / checklist.Length);
        var firstMissing = checklist.FirstOrDefault(c => !c.Done).Label;

        return (percent, percent >= 100 ? null : $"{firstMissing} to reach 100%");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(string fullName, string? displayName, string? district, int yearsOfExperience, string? teachingMode, string? languages, string? teachingStyle, string? bio)
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");

        if (string.IsNullOrWhiteSpace(fullName))
        {
            TempData["SettingsError"] = "Full name is required.";
            return RedirectToAction("Profile");
        }

        tutor.User.FullName = fullName.Trim();
        tutor.User.District = string.IsNullOrWhiteSpace(district) ? null : district;
        tutor.DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
        tutor.YearsOfExperience = yearsOfExperience;
        tutor.TeachingMode = string.IsNullOrWhiteSpace(teachingMode) ? null : teachingMode;
        tutor.Languages = languages;
        tutor.TeachingStyle = teachingStyle;
        tutor.Bio = string.IsNullOrWhiteSpace(bio) ? null : bio.Trim();

        await _context.SaveChangesAsync();

        TempData["SettingsSuccess"] = "Profile updated.";
        return RedirectToAction("Profile");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSubject(string subject, string? description)
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");

        if (string.IsNullOrWhiteSpace(subject))
        {
            TempData["SettingsError"] = "Subject is required.";
            return RedirectToAction("Profile");
        }

        var nextSortOrder = await _context.TutorSubjects
            .Where(s => s.TutorProfileId == tutor.Id)
            .Select(s => (int?)s.SortOrder)
            .MaxAsync() ?? -1;

        _context.TutorSubjects.Add(new TutorSubject
        {
            TutorProfileId = tutor.Id,
            Subject = subject.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            SortOrder = nextSortOrder + 1
        });

        await _context.SaveChangesAsync();
        TempData["SettingsSuccess"] = "Subject added.";
        return RedirectToAction("Profile");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSubject(int id, string subject, string? description)
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");

        var row = await _context.TutorSubjects
            .FirstOrDefaultAsync(s => s.Id == id && s.TutorProfileId == tutor.Id);

        if (row != null && !string.IsNullOrWhiteSpace(subject))
        {
            row.Subject = subject.Trim();
            row.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            await _context.SaveChangesAsync();
        }

        return RedirectToAction("Profile");
    }

    // ── UploadVerificationDocument / RemoveVerificationDocument ──────────

    public async Task<IActionResult> UploadVerificationDocument(IFormFile? file, string documentType)
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");

        if (!RequiredVerificationDocuments.Any(rd => rd.Type == documentType))
        {
            TempData["ProfileError"] = "Unknown document type.";
            return RedirectToAction(tutor.IsVerified ? "Profile" : "VerificationPending");
        }

        var (relativePath, originalFileName, sizeBytes, error) =
            await FileUploadHelper.SaveVerificationDocumentAsync(file, _webHostEnvironment.ContentRootPath, tutor.Id);

        if (error != null)
        {
            TempData["ProfileError"] = error;
            return RedirectToAction(tutor.IsVerified ? "Profile" : "VerificationPending");
        }

        var existing = await _context.TutorCredentials
            .FirstOrDefaultAsync(c => c.TutorProfileId == tutor.Id && c.DocumentType == documentType);

        if (existing != null)
        {
            FileUploadHelper.TryDeleteVerificationDocument(_webHostEnvironment.ContentRootPath, existing.FilePath);
            existing.FilePath = relativePath;
            existing.FileName = originalFileName;
            existing.FileSizeBytes = sizeBytes ?? 0;
            existing.UploadedAt = DateTime.Now;
        }
        else
        {
            var docMeta = RequiredVerificationDocuments.First(rd => rd.Type == documentType);
            var maxSortOrder = await _context.TutorCredentials
                .Where(c => c.TutorProfileId == tutor.Id)
                .Select(c => (int?)c.SortOrder)
                .MaxAsync() ?? -1;

            _context.TutorCredentials.Add(new TutorCredential
            {
                TutorProfileId = tutor.Id,
                Title = docMeta.Label,
                FileName = originalFileName,
                Icon = docMeta.Icon,
                SortOrder = maxSortOrder + 1,
                DocumentType = documentType,
                FilePath = relativePath,
                FileSizeBytes = sizeBytes ?? 0,
                UploadedAt = DateTime.Now
            });
        }

        if (tutor.VerificationRejected)
        {
            tutor.VerificationRejected = false;
            tutor.VerificationDecidedAt = null;
        }

        tutor.VerificationNote = null;

        // Fire "verification submitted" the moment the tutor's document set
        // becomes complete (not on every individual upload) - checks the
        // in-memory state we've been building this request, since the new
        // TutorCredential row may not be saved yet.
        if (!tutor.IsVerified && !tutor.VerificationRejected)
        {
            var currentDocTypes = (await _context.TutorCredentials
                .Where(c => c.TutorProfileId == tutor.Id)
                .Select(c => c.DocumentType)
                .ToListAsync())
                .Where(d => d != null)
                .ToHashSet();
            currentDocTypes.Add(documentType);

            var allComplete = RequiredVerificationDocuments.All(rd => currentDocTypes.Contains(rd.Type));
            if (allComplete)
            {
                NotificationHelper.Create(_context,
                    type: "Verification",
                    title: "New tutor verification submitted",
                    message: $"{tutor.User.FullName} submitted documents for {tutor.Subjects}",
                    icon: "🎓",
                    actionLabel: "Review now",
                    actionUrl: Url.Action("TutorVerification", "Admin"));
            }
        }

        await _context.SaveChangesAsync();

        TempData["ProfileSuccess"] = "Document uploaded.";
        return RedirectToAction(tutor.IsVerified ? "Profile" : "VerificationPending");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveVerificationDocument(int credentialId)
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");

        var credential = await _context.TutorCredentials
            .FirstOrDefaultAsync(c => c.Id == credentialId && c.TutorProfileId == tutor.Id);

        if (credential != null)
        {
            FileUploadHelper.TryDeleteVerificationDocument(_webHostEnvironment.ContentRootPath, credential.FilePath);
            _context.TutorCredentials.Remove(credential);
            await _context.SaveChangesAsync();
        }

        TempData["ProfileSuccess"] = "Document removed.";
        return RedirectToAction(tutor.IsVerified ? "Profile" : "VerificationPending");
    }

    // ── DownloadOwnVerificationDocument ──────────────────────────────────

    // Served inline so the browser renders the PDF/image directly in a new
    // tab rather than downloading it.
    public async Task<IActionResult> DownloadOwnVerificationDocument(int credentialId)
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");

        var credential = await _context.TutorCredentials
            .FirstOrDefaultAsync(c => c.Id == credentialId && c.TutorProfileId == tutor.Id);

        if (credential == null || string.IsNullOrWhiteSpace(credential.FilePath))
            return NotFound();

        var fullPath = FileUploadHelper.ResolveVerificationDocumentPath(_webHostEnvironment.ContentRootPath, credential.FilePath);
        if (!System.IO.File.Exists(fullPath))
            return NotFound();

        var bytes = await System.IO.File.ReadAllBytesAsync(fullPath);
        var displayName = credential.FileName ?? Path.GetFileName(fullPath);
        Response.Headers.Append("Content-Disposition", FileUploadHelper.BuildInlineContentDisposition(displayName));
        return File(bytes, FileUploadHelper.GetContentType(displayName));
    }

    // ── Settings ──────────────────────────────────────────────────────────

    private async Task BuildSettingsViewModelAsync(TutorProfile tutor) { }

    private async Task<TutorSettingsPageViewModel> BuildSettingsVmAsync(TutorProfile tutor)
    {
        return new TutorSettingsPageViewModel
        {
            Initials = GetInitials(tutor.User.FullName),
            Email = tutor.User.Email ?? "",
            PhoneNumber = tutor.User.PhoneNumber,
            TwoFactorEnabled = tutor.User.TwoFactorEnabled,
            ShowAvailabilityBadge = tutor.ShowAvailabilityBadge,
            AutoAcceptReturningStudents = tutor.AutoAcceptReturningStudents,
            MinimumBookingNoticeHours = tutor.MinimumBookingNoticeHours,
            CancellationWindowHours = tutor.CancellationWindowHours,
            MaxSessionsPerDay = tutor.MaxSessionsPerDay,
            NotifyNewSessionRequests = tutor.NotifyNewSessionRequests,
            NotifyNewMessages = tutor.NotifyNewMessages,
            NotifyWeeklyEarningsSummary = tutor.NotifyWeeklyEarningsSummary,
            IsListedInSearch = tutor.IsListedInSearch
        };
    }

    public async Task<IActionResult> SettingsAccount()
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");
        await SetTutorSidebarContextAsync("settings", tutor);
        return View(await BuildSettingsVmAsync(tutor));
    }

    public async Task<IActionResult> SettingsAvailability()
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");
        await SetTutorSidebarContextAsync("settings", tutor);
        return View(await BuildSettingsVmAsync(tutor));
    }

    public async Task<IActionResult> SettingsBookingPolicy()
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");
        await SetTutorSidebarContextAsync("settings", tutor);
        return View(await BuildSettingsVmAsync(tutor));
    }

    public async Task<IActionResult> SettingsNotifications()
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");
        await SetTutorSidebarContextAsync("settings", tutor);
        return View(await BuildSettingsVmAsync(tutor));
    }

    public async Task<IActionResult> SettingsPrivacy()
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");
        await SetTutorSidebarContextAsync("settings", tutor);
        return View(await BuildSettingsVmAsync(tutor));
    }

    public async Task<IActionResult> SettingsDevices()
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");
        await SetTutorSidebarContextAsync("settings", tutor);
        return View(await BuildSettingsVmAsync(tutor));
    }

    public async Task<IActionResult> SettingsCalendar()
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");
        await SetTutorSidebarContextAsync("settings", tutor);
        return View(await BuildSettingsVmAsync(tutor));
    }

    public async Task<IActionResult> SettingsDeactivate()
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");
        await SetTutorSidebarContextAsync("settings", tutor);
        return View(await BuildSettingsVmAsync(tutor));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAccount(string? email, string? phoneNumber)
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");

        var user = tutor.User;

        if (string.IsNullOrWhiteSpace(email))
        {
            TempData["SettingsError"] = "Email is required.";
            return RedirectToAction("SettingsAccount");
        }

        user.PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();

        if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            var setEmailResult = await _userManager.SetEmailAsync(user, email.Trim());
            if (!setEmailResult.Succeeded)
            {
                TempData["SettingsError"] = "Could not update email: " + string.Join(" ", setEmailResult.Errors.Select(e => e.Description));
                return RedirectToAction("SettingsAccount");
            }
            await _userManager.SetUserNameAsync(user, email.Trim());
        }

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            TempData["SettingsError"] = "Could not save account changes.";
            return RedirectToAction("SettingsAccount");
        }

        await _signInManager.RefreshSignInAsync(user);
        TempData["SettingsSuccess"] = "Account details updated.";
        return RedirectToAction("SettingsAccount");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmNewPassword)
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword != confirmNewPassword)
        {
            TempData["SettingsError"] = "New password and confirmation do not match.";
            return RedirectToAction("SettingsAccount");
        }

        var result = await _userManager.ChangePasswordAsync(tutor.User, currentPassword, newPassword);
        if (!result.Succeeded)
        {
            TempData["SettingsError"] = string.Join(" ", result.Errors.Select(e => e.Description));
            return RedirectToAction("SettingsAccount");
        }

        await _signInManager.RefreshSignInAsync(tutor.User);
        TempData["SettingsSuccess"] = "Password changed.";
        return RedirectToAction("SettingsAccount");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SignOutAllDevices()
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");

        await _userManager.UpdateSecurityStampAsync(tutor.User);
        await _signInManager.SignOutAsync();

        TempData["SettingsSuccessGlobal"] = "You've been signed out on all devices. Please log in again.";
        return RedirectToAction("TutorLogin", "Account");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAvailabilityPreference(string key, bool value)
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");

        switch (key)
        {
            case "ShowAvailabilityBadge": tutor.ShowAvailabilityBadge = value; break;
            case "AutoAcceptReturningStudents": tutor.AutoAcceptReturningStudents = value; break;
        }

        await _context.SaveChangesAsync();
        return RedirectToAction("SettingsAvailability");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateBookingPolicy(int minimumBookingNoticeHours, int cancellationWindowHours, int maxSessionsPerDay)
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");

        tutor.MinimumBookingNoticeHours = Math.Max(0, minimumBookingNoticeHours);
        tutor.CancellationWindowHours = Math.Max(0, cancellationWindowHours);
        tutor.MaxSessionsPerDay = Math.Max(0, maxSessionsPerDay);

        await _context.SaveChangesAsync();
        TempData["SettingsSuccess"] = "Booking policy updated.";
        return RedirectToAction("SettingsBookingPolicy");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateNotificationPreference(string key, bool value)
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");

        switch (key)
        {
            case "NewSessionRequests": tutor.NotifyNewSessionRequests = value; break;
            case "NewMessages": tutor.NotifyNewMessages = value; break;
            case "WeeklyEarningsSummary": tutor.NotifyWeeklyEarningsSummary = value; break;
        }

        await _context.SaveChangesAsync();
        return RedirectToAction("SettingsNotifications");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSearchVisibility(bool isListedInSearch)
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");

        tutor.IsListedInSearch = isListedInSearch;
        await _context.SaveChangesAsync();
        return RedirectToAction("SettingsPrivacy");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateAccount(string confirmText)
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");

        if (!string.Equals(confirmText?.Trim(), "DEACTIVATE", StringComparison.Ordinal))
        {
            TempData["SettingsError"] = "Type DEACTIVATE exactly to confirm.";
            return RedirectToAction("SettingsDeactivate");
        }

        tutor.IsDeactivated = true;
        tutor.IsListedInSearch = false;
        await _context.SaveChangesAsync();

        await _signInManager.SignOutAsync();
        TempData["SettingsSuccessGlobal"] = "Your tutor account has been deactivated. Contact support to reactivate it.";
        return RedirectToAction("TutorLogin", "Account");
    }

    public async Task<IActionResult> HelpSupport()
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("Index", "Home");

        await SetTutorSidebarContextAsync("help", tutor);

        var tickets = await _context.SupportTickets
            .Where(t => t.TutorProfileId == tutor.Id)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        var vm = new HelpSupportPageViewModel
        {
            MyTickets = tickets.Select(t => new SupportTicketRowViewModel
            {
                Id = t.Id,
                Category = t.Category,
                Subject = t.Subject,
                Message = t.Message,
                Status = t.Status,
                CreatedAt = t.CreatedAt
            }).ToList()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitSupportTicket(string category, string subject, string message, int? bookingId)
    {
        var tutor = await GetCurrentTutorProfileAsync();
        if (tutor == null) return RedirectToAction("TutorLogin", "Account");

        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(message))
        {
            TempData["SettingsError"] = "Please fill in both a subject and a message.";
            return RedirectToAction("HelpSupport");
        }

        var validCategories = new[] { "Booking", "Schedule", "Payments", "Account", "Other" };
        var resolvedCategory = validCategories.Contains(category) ? category : "Other";

        // Only accept the booking link if it's a real "Booking" complaint
        // about a session that actually belongs to this tutor.
        Booking? linkedBooking = null;
        if (resolvedCategory == "Booking" && bookingId.HasValue)
        {
            linkedBooking = await _context.Bookings
                .Include(b => b.StudentProfile).ThenInclude(s => s.User)
                .FirstOrDefaultAsync(b => b.Id == bookingId.Value && b.TutorProfileId == tutor.Id);
        }

        _context.SupportTickets.Add(new SupportTicket
        {
            TutorProfileId = tutor.Id,
            Category = resolvedCategory,
            Subject = subject.Trim(),
            Message = message.Trim(),
            Status = "Open",
            CreatedAt = DateTime.Now,
            BookingId = linkedBooking?.Id
        });

        if (linkedBooking != null)
        {
            linkedBooking.IsDisputed = true;
        }

        NotificationHelper.Create(_context,
            type: "Complaint",
            title: "New complaint filed — Medium severity",
            message: $"{tutor.User.FullName} reported: {subject.Trim()}" + (linkedBooking != null ? $" (against {linkedBooking.StudentProfile.User.FullName})" : ""),
            icon: "⚠️",
            actionLabel: "Investigate",
            actionUrl: Url.Action("Complaints", "Admin"));

        await _context.SaveChangesAsync();

        TempData["SettingsSuccess"] = "Your request has been submitted. We'll get back to you by email.";
        return RedirectToAction("HelpSupport");
    }
}