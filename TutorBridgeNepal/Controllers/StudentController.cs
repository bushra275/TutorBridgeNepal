using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutorBridgeNepal.Data;
using TutorBridgeNepal.Models;
using TutorBridgeNepal.ViewModels;

namespace TutorBridgeNepal.Controllers;

[Authorize(Roles = "Student")]
public class StudentController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public StudentController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
    {
        _context = context;
        _userManager = userManager;
        _signInManager = signInManager;
    }

    private static string GetInitials(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "?";
        if (parts.Length == 1) return parts[0][..Math.Min(2, parts[0].Length)].ToUpper();
        return $"{parts[0][0]}{parts[^1][0]}".ToUpper();
    }

    private async Task<StudentProfile?> GetCurrentStudentProfileAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return null;
        return await _context.StudentProfiles.FirstOrDefaultAsync(s => s.UserId == user.Id);
    }

    private async Task<ApplicationUser?> SetSidebarContextAsync(string activeNav)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return null;

        var studentProfile = await _context.StudentProfiles.FirstOrDefaultAsync(s => s.UserId == user.Id);
        var subMeta = string.Join(" · ", new[] { studentProfile?.GradeLevel, user.District }
            .Where(x => !string.IsNullOrWhiteSpace(x)));

        var unreadCount = studentProfile == null
            ? 0
            : await _context.Messages.CountAsync(m =>
                m.StudentProfileId == studentProfile.Id && m.SenderRole == "Tutor" && !m.IsRead);

        ViewData["SidebarName"] = user.FullName;
        ViewData["SidebarInitials"] = GetInitials(user.FullName);
        ViewData["SidebarMeta"] = string.IsNullOrWhiteSpace(subMeta) ? "Student" : subMeta;
        ViewData["ActiveNav"] = activeNav;
        ViewData["UnreadMessageCount"] = unreadCount;

        var notifications = new List<NotificationItemViewModel>();

        if (studentProfile != null)
        {
            var now = DateTime.Now;

            // Sessions starting within the next 24 hours - most urgent, shown first
            var soonSessions = await _context.Bookings
                .Include(b => b.TutorProfile).ThenInclude(t => t.User)
                .Include(b => b.TutorAvailabilitySlot)
                .Where(b => b.StudentProfileId == studentProfile.Id
                    && b.Status == "Confirmed"
                    && b.TutorAvailabilitySlot.StartTime >= now
                    && b.TutorAvailabilitySlot.StartTime <= now.AddHours(24))
                .OrderBy(b => b.TutorAvailabilitySlot.StartTime)
                .Take(5)
                .ToListAsync();

            notifications.AddRange(soonSessions.Select(b => new NotificationItemViewModel
            {
                Type = "Session",
                Icon = "📅",
                Title = $"{b.Subject} with {b.TutorProfile.User.FullName}",
                Subtitle = $"Starts {b.TutorAvailabilitySlot.StartTime:ddd, d MMM h:mm tt}",
                Timestamp = b.TutorAvailabilitySlot.StartTime,
                LinkController = "Student",
                LinkAction = "Sessions"
            }));

            // Unread messages, one entry per tutor with the most recent unread one
            var unreadMessages = await _context.Messages
                .Include(m => m.TutorProfile).ThenInclude(t => t.User)
                .Where(m => m.StudentProfileId == studentProfile.Id && m.SenderRole == "Tutor" && !m.IsRead)
                .OrderByDescending(m => m.SentAt)
                .ToListAsync();

            notifications.AddRange(unreadMessages
                .GroupBy(m => m.TutorProfileId)
                .Select(g => g.First())
                .Take(5)
                .Select(m => new NotificationItemViewModel
                {
                    Type = "Message",
                    Icon = "💬",
                    Title = $"New message from {m.TutorProfile.User.FullName}",
                    Subtitle = m.Content.Length > 60 ? m.Content.Substring(0, 60) + "…" : m.Content,
                    Timestamp = m.SentAt,
                    LinkController = "Student",
                    LinkAction = "Messages",
                    RouteId = m.TutorProfileId
                }));

            // Goals due within the next 3 days (includes overdue) and not completed
            var dueSoonGoals = await _context.Goals
                .Where(g => g.StudentProfileId == studentProfile.Id
                    && g.Status != "Completed"
                    && g.DueDate != null
                    && g.DueDate <= now.AddDays(3))
                .OrderBy(g => g.DueDate)
                .Take(5)
                .ToListAsync();

            notifications.AddRange(dueSoonGoals.Select(g => new NotificationItemViewModel
            {
                Type = "Goal",
                Icon = "🎯",
                Title = g.Description,
                Subtitle = g.DueDate!.Value.Date < now.Date ? "Overdue" : $"Due {g.DueDate.Value:d MMM}",
                Timestamp = g.DueDate!.Value,
                LinkController = "Student",
                LinkAction = "Progress"
            }));

            // Achievements unlocked in the last 7 days
            var recentAchievements = await _context.StudentAchievements
                .Where(a => a.StudentProfileId == studentProfile.Id && a.UnlockedAt >= now.AddDays(-7))
                .OrderByDescending(a => a.UnlockedAt)
                .Take(5)
                .ToListAsync();

            notifications.AddRange(recentAchievements.Select(a =>
            {
                var meta = AchievementCatalog.Items.TryGetValue(a.AchievementKey, out var m) ? m : (Title: "Achievement", Icon: "🏆"); return new NotificationItemViewModel
                {
                    Type = "Achievement",
                    Icon = meta.Icon,
                    Title = $"Achievement unlocked: {meta.Title}",
                    Subtitle = $"Earned {a.UnlockedAt:d MMM}",
                    Timestamp = a.UnlockedAt,
                    LinkController = "Student",
                    LinkAction = "Progress"
                };
            }));
        }

        ViewData["Notifications"] = notifications.Take(8).ToList();
        ViewData["NotificationCount"] = notifications.Count;

        return user;
    }

    public async Task<IActionResult> Dashboard()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("StudentLogin", "Account");

        var studentProfile = await _context.StudentProfiles
            .FirstOrDefaultAsync(s => s.UserId == user.Id);

        if (studentProfile == null)
        {
            return RedirectToAction("Index", "Home");
        }

        await SetSidebarContextAsync("dashboard");

        var now = DateTime.Now;

        var bookings = await _context.Bookings
            .Include(b => b.TutorProfile).ThenInclude(t => t.User)
            .Include(b => b.TutorAvailabilitySlot)
            .Where(b => b.StudentProfileId == studentProfile.Id)
            .ToListAsync();

        var upcoming = bookings
            .Where(b => b.TutorAvailabilitySlot.StartTime >= now && b.Status != "Cancelled")
            .OrderBy(b => b.TutorAvailabilitySlot.StartTime)
            .ToList();

        var recent = bookings
            .Where(b => b.TutorAvailabilitySlot.StartTime < now || b.Status == "Cancelled")
            .OrderByDescending(b => b.TutorAvailabilitySlot.StartTime)
            .ToList();

        var completed = bookings.Where(b => b.Status == "Completed").ToList();

        double hoursLearned = completed
            .Sum(b => (b.TutorAvailabilitySlot.EndTime - b.TutorAvailabilitySlot.StartTime).TotalHours);

        var myTutors = bookings
            .Select(b => b.TutorProfile)
            .GroupBy(t => t.Id)
            .Select(g => g.First())
            .ToList();

        var subjectProgress = completed
            .GroupBy(b => b.Subject)
            .Select(g => new SubjectProgressViewModel
            {
                Subject = g.Key,
                CompletedSessions = g.Count()
            })
            .OrderByDescending(s => s.CompletedSessions)
            .ToList();

        var recentMessages = await _context.Messages
            .Include(m => m.TutorProfile).ThenInclude(t => t.User)
            .Where(m => m.StudentProfileId == studentProfile.Id)
            .OrderByDescending(m => m.SentAt)
            .Take(5)
            .ToListAsync();

        var vm = new StudentDashboardViewModel
        {
            FullName = user.FullName,
            Initials = GetInitials(user.FullName),
            GradeLevel = studentProfile.GradeLevel,
            District = user.District,
            TotalSessions = bookings.Count,
            CompletedSessions = completed.Count,
            HoursLearned = Math.Round(hoursLearned, 1),
            ActiveTutorsCount = myTutors.Count,
            UpcomingCount = upcoming.Count,
            UpcomingSessions = upcoming.Take(5).Select(b => new BookingRowViewModel
            {
                BookingId = b.Id,
                TutorName = b.TutorProfile.User.FullName,
                TutorInitials = GetInitials(b.TutorProfile.User.FullName),
                Subject = b.Subject,
                StartTime = b.TutorAvailabilitySlot.StartTime,
                EndTime = b.TutorAvailabilitySlot.EndTime,
                Status = b.Status
            }).ToList(),
            RecentSessions = recent.Take(5).Select(b => new BookingRowViewModel
            {
                BookingId = b.Id,
                TutorName = b.TutorProfile.User.FullName,
                TutorInitials = GetInitials(b.TutorProfile.User.FullName),
                Subject = b.Subject,
                StartTime = b.TutorAvailabilitySlot.StartTime,
                EndTime = b.TutorAvailabilitySlot.EndTime,
                Status = b.Status
            }).ToList(),
            MyTutors = myTutors.Select(t => new TutorRowViewModel
            {
                TutorProfileId = t.Id,
                TutorName = t.User.FullName,
                TutorInitials = GetInitials(t.User.FullName),
                Subjects = t.Subjects,
                AverageRating = t.AverageRating
            }).ToList(),
            SubjectProgress = subjectProgress,
            RecentMessages = recentMessages.Select(m => new RecentMessageViewModel
            {
                TutorProfileId = m.TutorProfileId,
                TutorName = m.TutorProfile.User.FullName,
                TutorInitials = GetInitials(m.TutorProfile.User.FullName),
                Preview = m.Content,
                SentAt = m.SentAt,
                IsFromStudent = m.SenderRole == "Student"
            }).ToList()
        };

        return View(vm);
    }

    public async Task<IActionResult> FindTutors(string? q, string? subject, string? district, string sort = "rating", int page = 1)
    {
        await SetSidebarContextAsync("findtutors");

        var baseQuery = _context.TutorProfiles.Include(t => t.User).Where(t => t.IsVerified);

        var allVerified = await baseQuery.ToListAsync();
        var subjectOptions = allVerified
            .SelectMany(t => t.Subjects.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct()
            .OrderBy(s => s)
            .ToList();
        var districtOptions = allVerified
            .Select(t => t.User.District)
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Distinct()
            .OrderBy(d => d)
            .ToList()!;

        var filtered = baseQuery.AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            filtered = filtered.Where(t =>
                t.Subjects.Contains(term) ||
                t.User.FullName.Contains(term) ||
                (t.User.District != null && t.User.District.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(subject))
        {
            filtered = filtered.Where(t => t.Subjects.Contains(subject));
        }

        if (!string.IsNullOrWhiteSpace(district))
        {
            filtered = filtered.Where(t => t.User.District == district);
        }

        filtered = sort switch
        {
            "experience" => filtered.OrderByDescending(t => t.YearsOfExperience),
            _ => filtered.OrderByDescending(t => t.AverageRating)
        };

        var totalCount = await filtered.CountAsync();
        var pageSize = 6;
        page = Math.Max(1, page);

        var pageItems = await filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var pageTutorIds = pageItems.Select(t => t.Id).ToList();
        var completedCounts = await _context.Bookings
            .Where(b => pageTutorIds.Contains(b.TutorProfileId) && b.Status == "Completed")
            .GroupBy(b => b.TutorProfileId)
            .Select(g => new { TutorProfileId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TutorProfileId, x => x.Count);

        var vm = new StudentFindTutorsViewModel
        {
            Query = q,
            Subject = subject,
            District = district,
            Sort = sort,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            SubjectOptions = subjectOptions,
            DistrictOptions = districtOptions,
            Tutors = pageItems.Select(t => new TutorSummaryViewModel
            {
                TutorProfileId = t.Id,
                FullName = t.User.FullName,
                Initials = GetInitials(t.User.FullName),
                Subjects = t.Subjects,
                District = t.User.District,
                YearsOfExperience = t.YearsOfExperience,
                AverageRating = t.AverageRating,
                IsVerified = t.IsVerified,
                CompletedSessionsCount = completedCounts.GetValueOrDefault(t.Id, 0)
            }).ToList()
        };

        return View(vm);
    }

    public async Task<IActionResult> TutorProfile(int id)
    {
        await SetSidebarContextAsync("findtutors");

        var tutor = await _context.TutorProfiles
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Id == id && t.IsVerified);

        if (tutor == null) return NotFound();

        var now = DateTime.Now;
        var slots = await _context.TutorAvailabilitySlots
            .Where(s => s.TutorProfileId == id && !s.IsBooked && s.StartTime >= now)
            .OrderBy(s => s.StartTime)
            .Take(60)
            .ToListAsync();

        var completedSessionsCount = await _context.Bookings
            .CountAsync(b => b.TutorProfileId == id && b.Status == "Completed");

        var vm = new TutorProfileDetailViewModel
        {
            TutorProfileId = tutor.Id,
            FullName = tutor.User.FullName,
            Initials = GetInitials(tutor.User.FullName),
            Subjects = tutor.Subjects,
            District = tutor.User.District,
            Bio = tutor.Bio,
            TeachingStyle = tutor.TeachingStyle,
            YearsOfExperience = tutor.YearsOfExperience,
            AverageRating = tutor.AverageRating,
            IsVerified = tutor.IsVerified,
            CompletedSessionsCount = completedSessionsCount,
            AvailableSlots = slots.Select(s => new AvailableSlotViewModel
            {
                SlotId = s.Id,
                StartTime = s.StartTime,
                EndTime = s.EndTime
            }).ToList()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BookSlot(int slotId, string subject, int tutorProfileId)
    {
        var studentProfile = await GetCurrentStudentProfileAsync();
        if (studentProfile == null) return RedirectToAction("StudentLogin", "Account");

        var slot = await _context.TutorAvailabilitySlots
            .FirstOrDefaultAsync(s => s.Id == slotId && !s.IsBooked);

        if (slot == null)
        {
            TempData["BookingError"] = "That slot is no longer available. Please choose another time.";
            return RedirectToAction("TutorProfile", new { id = tutorProfileId });
        }

        _context.Bookings.Add(new Booking
        {
            StudentProfileId = studentProfile.Id,
            TutorProfileId = slot.TutorProfileId,
            TutorAvailabilitySlotId = slot.Id,
            Subject = string.IsNullOrWhiteSpace(subject) ? "General" : subject,
            Status = "Pending"
        });

        slot.IsBooked = true;
        await _context.SaveChangesAsync();

        TempData["BookingSuccess"] = "Session requested! Your tutor will confirm it shortly.";
        return RedirectToAction("Sessions");
    }

    public async Task<IActionResult> Sessions(string tab = "upcoming", string? subject = null, int? tutorProfileId = null, string sort = "newest")
    {
        var studentProfile = await GetCurrentStudentProfileAsync();
        if (studentProfile == null) return RedirectToAction("Index", "Home");

        await SetSidebarContextAsync("sessions");

        var now = DateTime.Now;

        var allBookings = await _context.Bookings
            .Include(b => b.TutorProfile).ThenInclude(t => t.User)
            .Include(b => b.TutorAvailabilitySlot)
            .Where(b => b.StudentProfileId == studentProfile.Id)
            .ToListAsync();

        bool IsUpcoming(Booking b) => b.TutorAvailabilitySlot.StartTime >= now && b.Status != "Cancelled";
        bool IsCompleted(Booking b) => b.Status == "Completed";
        bool IsCancelled(Booking b) => b.Status == "Cancelled";

        var upcomingList = allBookings.Where(IsUpcoming).ToList();
        var completedList = allBookings.Where(IsCompleted).ToList();
        var cancelledList = allBookings.Where(IsCancelled).ToList();

        IEnumerable<Booking> scoped = tab switch
        {
            "completed" => completedList,
            "cancelled" => cancelledList,
            "all" => allBookings,
            _ => upcomingList
        };

        if (!string.IsNullOrWhiteSpace(subject))
        {
            scoped = scoped.Where(b => b.Subject == subject);
        }

        if (tutorProfileId.HasValue)
        {
            scoped = scoped.Where(b => b.TutorProfileId == tutorProfileId.Value);
        }

        scoped = sort switch
        {
            "oldest" => scoped.OrderBy(b => b.TutorAvailabilitySlot.StartTime),
            _ => scoped.OrderByDescending(b => b.TutorAvailabilitySlot.StartTime)
        };

        var hoursLearned = completedList
            .Sum(b => (b.TutorAvailabilitySlot.EndTime - b.TutorAvailabilitySlot.StartTime).TotalHours);

        var decided = completedList.Count + cancelledList.Count;
        var completionRate = decided == 0 ? 0 : (int)Math.Round(completedList.Count * 100.0 / decided);

        var completedBookingIds = completedList.Select(b => b.Id).ToList();
        var myReviewRatings = await _context.Reviews
            .Where(r => r.StudentProfileId == studentProfile.Id && completedBookingIds.Contains(r.BookingId))
            .ToDictionaryAsync(r => r.BookingId, r => r.Rating);

        var vm = new StudentSessionsViewModel
        {
            ActiveTab = tab,
            Subject = subject,
            TutorProfileId = tutorProfileId,
            Sort = sort,
            Sessions = scoped.Select(b => new SessionRowViewModel
            {
                BookingId = b.Id,
                TutorProfileId = b.TutorProfileId,
                TutorName = b.TutorProfile.User.FullName,
                TutorInitials = GetInitials(b.TutorProfile.User.FullName),
                Subject = b.Subject,
                StartTime = b.TutorAvailabilitySlot.StartTime,
                EndTime = b.TutorAvailabilitySlot.EndTime,
                Status = b.Status,
                MyReviewRating = myReviewRatings.TryGetValue(b.Id, out var myRating) ? myRating : (int?)null
            }).ToList(),
            SubjectOptions = allBookings.Select(b => b.Subject).Distinct().OrderBy(s => s).ToList(),
            TutorOptions = allBookings
                .Select(b => b.TutorProfile)
                .GroupBy(t => t.Id)
                .Select(g => g.First())
                .Select(t => new TutorRowViewModel
                {
                    TutorProfileId = t.Id,
                    TutorName = t.User.FullName,
                    TutorInitials = GetInitials(t.User.FullName),
                    Subjects = t.Subjects,
                    AverageRating = t.AverageRating
                }).ToList(),
            UpcomingCount = upcomingList.Count,
            CompletedCount = completedList.Count,
            CancelledCount = cancelledList.Count,
            AllCount = allBookings.Count,
            TotalSessions = allBookings.Count,
            HoursLearned = Math.Round(hoursLearned, 1),
            CompletionRatePercent = completionRate,
            DecidedSessionsCount = decided
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelBooking(int id, string returnTo = "Dashboard")
    {
        var studentProfile = await GetCurrentStudentProfileAsync();
        if (studentProfile == null) return RedirectToAction("StudentLogin", "Account");

        var booking = await _context.Bookings
            .Include(b => b.TutorAvailabilitySlot)
            .FirstOrDefaultAsync(b => b.Id == id && b.StudentProfileId == studentProfile.Id);

        if (booking != null && booking.Status != "Completed" && booking.Status != "Cancelled")
        {
            booking.Status = "Cancelled";
            booking.TutorAvailabilitySlot.IsBooked = false;
            await _context.SaveChangesAsync();
        }

        return returnTo == "Sessions" ? RedirectToAction("Sessions") : RedirectToAction("Dashboard");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitReview(int bookingId, int rating, string? comment)
    {
        var studentProfile = await GetCurrentStudentProfileAsync();
        if (studentProfile == null) return RedirectToAction("StudentLogin", "Account");

        if (rating < 1 || rating > 5)
        {
            TempData["SettingsError"] = "Rating must be between 1 and 5 stars.";
            return RedirectToAction("Sessions", new { tab = "completed" });
        }

        var booking = await _context.Bookings
            .FirstOrDefaultAsync(b => b.Id == bookingId && b.StudentProfileId == studentProfile.Id && b.Status == "Completed");

        if (booking == null)
        {
            TempData["SettingsError"] = "That session can't be reviewed.";
            return RedirectToAction("Sessions", new { tab = "completed" });
        }

        var alreadyReviewed = await _context.Reviews.AnyAsync(r => r.BookingId == bookingId);
        if (alreadyReviewed)
        {
            return RedirectToAction("Sessions", new { tab = "completed" });
        }

        _context.Reviews.Add(new Review
        {
            BookingId = booking.Id,
            StudentProfileId = studentProfile.Id,
            TutorProfileId = booking.TutorProfileId,
            Rating = rating,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
            CreatedAt = DateTime.Now
        });
        await _context.SaveChangesAsync();

        // Recompute the tutor's real average from actual reviews - no more
        // manually-set placeholder ratings once a genuine review exists.
        var tutor = await _context.TutorProfiles.FirstOrDefaultAsync(t => t.Id == booking.TutorProfileId);
        if (tutor != null)
        {
            var tutorRatings = await _context.Reviews
                .Where(r => r.TutorProfileId == tutor.Id)
                .Select(r => r.Rating)
                .ToListAsync();

            tutor.ReviewCount = tutorRatings.Count;
            tutor.AverageRating = tutorRatings.Count == 0 ? 0m : Math.Round((decimal)tutorRatings.Average(), 2);
            await _context.SaveChangesAsync();
        }

        TempData["SettingsSuccess"] = "Thanks for your review!";
        return RedirectToAction("Sessions", new { tab = "completed" });
    }

    public async Task<IActionResult> Messages(int? tutorProfileId)
    {
        var studentProfile = await GetCurrentStudentProfileAsync();
        if (studentProfile == null) return RedirectToAction("Index", "Home");

        await SetSidebarContextAsync("messages");

        // Conversations = tutors this student has actually booked (a real relationship),
        // same set as "My Tutors" - you can only message tutors you've booked.
        var myTutors = await _context.Bookings
            .Include(b => b.TutorProfile).ThenInclude(t => t.User)
            .Where(b => b.StudentProfileId == studentProfile.Id)
            .Select(b => b.TutorProfile)
            .Distinct()
            .ToListAsync();

        var tutorIds = myTutors.Select(t => t.Id).ToList();

        var allMessages = await _context.Messages
            .Where(m => m.StudentProfileId == studentProfile.Id && tutorIds.Contains(m.TutorProfileId))
            .OrderByDescending(m => m.SentAt)
            .ToListAsync();

        var conversations = myTutors.Select(t =>
        {
            var threadMessages = allMessages.Where(m => m.TutorProfileId == t.Id).ToList();
            var last = threadMessages.FirstOrDefault();
            return new ConversationListItemViewModel
            {
                TutorProfileId = t.Id,
                TutorName = t.User.FullName,
                TutorInitials = GetInitials(t.User.FullName),
                Subjects = t.Subjects,
                LastMessagePreview = last?.Content,
                LastMessageAt = last?.SentAt,
                UnreadCount = threadMessages.Count(m => m.SenderRole == "Tutor" && !m.IsRead)
            };
        })
        .OrderByDescending(c => c.LastMessageAt ?? DateTime.MinValue)
        .ToList();

        var activeTutorId = tutorProfileId ?? conversations.FirstOrDefault()?.TutorProfileId;

        var vm = new MessagesPageViewModel
        {
            Conversations = conversations,
            TotalUnread = conversations.Sum(c => c.UnreadCount)
        };

        if (activeTutorId.HasValue)
        {
            var activeTutor = myTutors.FirstOrDefault(t => t.Id == activeTutorId.Value);
            if (activeTutor != null)
            {
                var threadMessages = allMessages
                    .Where(m => m.TutorProfileId == activeTutorId.Value)
                    .OrderBy(m => m.SentAt)
                    .ToList();

                // Mark unread tutor messages in this thread as read now that the student is viewing it.
                var unreadFromTutor = threadMessages.Where(m => m.SenderRole == "Tutor" && !m.IsRead).ToList();
                if (unreadFromTutor.Any())
                {
                    var idsToMark = unreadFromTutor.Select(m => m.Id).ToList();
                    var toUpdate = await _context.Messages.Where(m => idsToMark.Contains(m.Id)).ToListAsync();
                    foreach (var m in toUpdate) m.IsRead = true;
                    await _context.SaveChangesAsync();
                }

                vm.ActiveTutorProfileId = activeTutor.Id;
                vm.ActiveTutorName = activeTutor.User.FullName;
                vm.ActiveTutorInitials = GetInitials(activeTutor.User.FullName);
                vm.ActiveTutorSubjects = activeTutor.Subjects;
                vm.Messages = threadMessages.Select(m => new MessageBubbleViewModel
                {
                    Id = m.Id,
                    SenderRole = m.SenderRole,
                    Content = m.Content,
                    SentAt = m.SentAt,
                    IsRead = m.IsRead
                }).ToList();

                // Recompute unread count for the sidebar list now that this thread is read.
                vm.Conversations.First(c => c.TutorProfileId == activeTutorId.Value).UnreadCount = 0;
                vm.TotalUnread = vm.Conversations.Sum(c => c.UnreadCount);
            }
        }

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendMessage(int tutorProfileId, string content)
    {
        var studentProfile = await GetCurrentStudentProfileAsync();
        if (studentProfile == null) return RedirectToAction("StudentLogin", "Account");

        if (string.IsNullOrWhiteSpace(content))
        {
            return RedirectToAction("Messages", new { tutorProfileId });
        }

        // Only allow messaging a tutor the student actually has a booking relationship with.
        var hasRelationship = await _context.Bookings
            .AnyAsync(b => b.StudentProfileId == studentProfile.Id && b.TutorProfileId == tutorProfileId);

        if (!hasRelationship) return RedirectToAction("Messages");

        _context.Messages.Add(new Message
        {
            StudentProfileId = studentProfile.Id,
            TutorProfileId = tutorProfileId,
            SenderRole = "Student",
            Content = content.Trim(),
            SentAt = DateTime.Now,
            IsRead = false
        });

        await _context.SaveChangesAsync();

        return RedirectToAction("Messages", new { tutorProfileId });
    }
    public async Task<IActionResult> MyTutors(string tab = "active", string? subject = null, string sort = "recent")
    {
        var studentProfile = await GetCurrentStudentProfileAsync();
        if (studentProfile == null) return RedirectToAction("Index", "Home");

        await SetSidebarContextAsync("tutors");

        var now = DateTime.Now;

        var bookings = await _context.Bookings
            .Include(b => b.TutorProfile).ThenInclude(t => t.User)
            .Include(b => b.TutorAvailabilitySlot)
            .Where(b => b.StudentProfileId == studentProfile.Id)
            .ToListAsync();

        var savedTutorIds = await _context.SavedTutors
            .Where(s => s.StudentProfileId == studentProfile.Id)
            .Select(s => s.TutorProfileId)
            .ToListAsync();

        var bookedTutorIds = bookings.Select(b => b.TutorProfileId).Distinct().ToList();
        var allRelevantTutorIds = bookedTutorIds.Union(savedTutorIds).ToList();

        var allRelevantTutors = await _context.TutorProfiles
            .Include(t => t.User)
            .Where(t => allRelevantTutorIds.Contains(t.Id))
            .ToListAsync();

        MyTutorCardViewModel BuildCard(TutorProfile tutor)
        {
            var tutorBookings = bookings.Where(b => b.TutorProfileId == tutor.Id).ToList();
            var upcoming = tutorBookings
                .Where(b => b.TutorAvailabilitySlot.StartTime >= now && b.Status != "Cancelled")
                .OrderBy(b => b.TutorAvailabilitySlot.StartTime)
                .ToList();
            var past = tutorBookings
                .Where(b => b.TutorAvailabilitySlot.StartTime < now)
                .OrderByDescending(b => b.TutorAvailabilitySlot.StartTime)
                .ToList();

            return new MyTutorCardViewModel
            {
                TutorProfileId = tutor.Id,
                FullName = tutor.User.FullName,
                Initials = GetInitials(tutor.User.FullName),
                Subjects = tutor.Subjects,
                District = tutor.User.District,
                YearsOfExperience = tutor.YearsOfExperience,
                AverageRating = tutor.AverageRating,
                IsVerified = tutor.IsVerified,
                IsSaved = savedTutorIds.Contains(tutor.Id),
                SessionsWithStudent = tutorBookings.Count(b => b.Status != "Cancelled"),
                LastSessionAt = past.FirstOrDefault()?.TutorAvailabilitySlot.StartTime,
                NextSessionAt = upcoming.FirstOrDefault()?.TutorAvailabilitySlot.StartTime
            };
        }

        var allCards = allRelevantTutors.Select(BuildCard).ToList();

        var activeCards = allCards.Where(c => c.NextSessionAt.HasValue).ToList();
        var savedCards = allCards.Where(c => c.IsSaved).ToList();
        var pastCards = allCards.Where(c => c.SessionsWithStudent > 0 && !c.NextSessionAt.HasValue).ToList();

        IEnumerable<MyTutorCardViewModel> scoped = tab switch
        {
            "saved" => savedCards,
            "past" => pastCards,
            "all" => allCards,
            _ => activeCards
        };

        if (!string.IsNullOrWhiteSpace(subject))
        {
            scoped = scoped.Where(c => c.SubjectTags.Contains(subject));
        }

        scoped = sort switch
        {
            "name" => scoped.OrderBy(c => c.FullName),
            "rating" => scoped.OrderByDescending(c => c.AverageRating),
            _ => scoped.OrderByDescending(c => c.LastSessionAt ?? c.NextSessionAt ?? DateTime.MinValue)
        };

        var completed = bookings.Where(b => b.Status == "Completed").ToList();
        var hoursLearned = completed.Sum(b => (b.TutorAvailabilitySlot.EndTime - b.TutorAvailabilitySlot.StartTime).TotalHours);

        var vm = new MyTutorsPageViewModel
        {
            ActiveTab = tab,
            Subject = subject,
            Sort = sort,
            ActiveTutorsCount = activeCards.Count,
            SavedTutorsCount = savedCards.Count,
            PastTutorsCount = pastCards.Count,
            AllTutorsCount = allCards.Count,
            SessionsDoneCount = completed.Count,
            HoursLearned = Math.Round(hoursLearned, 1),
            Tutors = scoped.ToList(),
            SavedPreview = savedCards.Take(3).ToList(),
            SubjectOptions = allCards.SelectMany(c => c.SubjectTags).Distinct().OrderBy(s => s).ToList(),
            RecentHistory = completed
                .OrderByDescending(b => b.TutorAvailabilitySlot.StartTime)
                .Take(5)
                .Select(b => new TutorSessionHistoryRow
                {
                    TutorProfileId = b.TutorProfileId,
                    TutorName = b.TutorProfile.User.FullName,
                    TutorInitials = GetInitials(b.TutorProfile.User.FullName),
                    Subject = b.Subject,
                    StartTime = b.TutorAvailabilitySlot.StartTime,
                    EndTime = b.TutorAvailabilitySlot.EndTime,
                    Status = b.Status
                }).ToList()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveTutor(int tutorProfileId, string returnTo = "FindTutors")
    {
        var studentProfile = await GetCurrentStudentProfileAsync();
        if (studentProfile == null) return RedirectToAction("StudentLogin", "Account");

        var tutor = await _context.TutorProfiles
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Id == tutorProfileId);

        if (tutor == null) return RedirectToAction(returnTo == "TutorProfile" ? "FindTutors" : returnTo);

        var alreadySaved = await _context.SavedTutors
            .AnyAsync(s => s.StudentProfileId == studentProfile.Id && s.TutorProfileId == tutorProfileId);

        if (!alreadySaved)
        {
            _context.SavedTutors.Add(new SavedTutor
            {
                StudentProfileId = studentProfile.Id,
                TutorProfileId = tutorProfileId
            });
            await _context.SaveChangesAsync();
        }

        TempData["SavedMessage"] = $"{tutor.User.FullName} has been saved";

        return returnTo switch
        {
            "TutorProfile" => RedirectToAction("TutorProfile", new { id = tutorProfileId }),
            "MyTutors" => RedirectToAction("MyTutors"),
            _ => RedirectToAction("FindTutors")
        };
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnsaveTutor(int tutorProfileId, string returnTo = "MyTutors")
    {
        var studentProfile = await GetCurrentStudentProfileAsync();
        if (studentProfile == null) return RedirectToAction("StudentLogin", "Account");

        var existing = await _context.SavedTutors
            .FirstOrDefaultAsync(s => s.StudentProfileId == studentProfile.Id && s.TutorProfileId == tutorProfileId);

        if (existing != null)
        {
            _context.SavedTutors.Remove(existing);
            await _context.SaveChangesAsync();
        }

        return returnTo switch
        {
            "TutorProfile" => RedirectToAction("TutorProfile", new { id = tutorProfileId }),
            "FindTutors" => RedirectToAction("FindTutors"),
            _ => RedirectToAction("MyTutors")
        };
    }

    public async Task<IActionResult> Progress()
    {
        var studentProfile = await GetCurrentStudentProfileAsync();
        if (studentProfile == null) return RedirectToAction("Index", "Home");

        await SetSidebarContextAsync("progress");

        var now = DateTime.Now;
        var today = DateTime.Today;

        var bookings = await _context.Bookings
            .Include(b => b.TutorProfile).ThenInclude(t => t.User)
            .Include(b => b.TutorAvailabilitySlot)
            .Where(b => b.StudentProfileId == studentProfile.Id)
            .ToListAsync();

        var completed = bookings.Where(b => b.Status == "Completed").ToList();

        double totalHoursLearned = completed
            .Sum(b => (b.TutorAvailabilitySlot.EndTime - b.TutorAvailabilitySlot.StartTime).TotalHours);

        // Monday..Sunday of the current calendar week
        int diffToMonday = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var weekStart = today.AddDays(-diffToMonday);
        var weekEnd = weekStart.AddDays(7);

        var completedThisWeek = completed
            .Where(b => b.TutorAvailabilitySlot.StartTime >= weekStart && b.TutorAvailabilitySlot.StartTime < weekEnd)
            .ToList();
        double hoursThisWeek = completedThisWeek
            .Sum(b => (b.TutorAvailabilitySlot.EndTime - b.TutorAvailabilitySlot.StartTime).TotalHours);

        var completedThisMonth = completed
            .Where(b => b.TutorAvailabilitySlot.StartTime.Year == now.Year && b.TutorAvailabilitySlot.StartTime.Month == now.Month)
            .ToList();

        var subjectsActiveCount = bookings
            .Where(b => b.Status != "Cancelled")
            .Select(b => b.Subject)
            .Distinct()
            .Count();

        // Per-subject cards: sessions/hours completed, and % of that subject's
        // non-cancelled bookings that were completed (a real completion rate,
        // not a fabricated mastery score - there is no topic-level data model).
        var subjectCards = completed
            .GroupBy(b => b.Subject)
            .Select(g =>
            {
                var subjectBookings = bookings.Where(b => b.Subject == g.Key && b.Status != "Cancelled").ToList();
                var totalForSubject = subjectBookings.Count;
                var completedForSubject = g.Count();
                var lastTutorName = g.OrderByDescending(b => b.TutorAvailabilitySlot.StartTime).First().TutorProfile.User.FullName;

                return new SubjectProgressCardViewModel
                {
                    Subject = g.Key,
                    TutorName = lastTutorName,
                    SessionsCount = completedForSubject,
                    HoursLearned = Math.Round(g.Sum(b => (b.TutorAvailabilitySlot.EndTime - b.TutorAvailabilitySlot.StartTime).TotalHours), 1),
                    CompletionRatePercent = totalForSubject == 0 ? 0 : (int)Math.Round(completedForSubject * 100.0 / totalForSubject)
                };
            })
            .OrderByDescending(s => s.SessionsCount)
            .ToList();

        // Weekly hours: Mon-Sun of the current week
        var weeklyHours = new List<WeeklyHoursPointViewModel>();
        string[] dayLabels = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
        for (int i = 0; i < 7; i++)
        {
            var day = weekStart.AddDays(i);
            var hoursOnDay = completed
                .Where(b => b.TutorAvailabilitySlot.StartTime.Date == day)
                .Sum(b => (b.TutorAvailabilitySlot.EndTime - b.TutorAvailabilitySlot.StartTime).TotalHours);
            weeklyHours.Add(new WeeklyHoursPointViewModel { DayLabel = dayLabels[i], Hours = Math.Round(hoursOnDay, 1) });
        }

        // Monthly trend: completed sessions per month, last 6 months, top 3 subjects
        var topSubjects = subjectCards.Take(3).Select(s => s.Subject).ToList();
        var monthlyTrend = new List<MonthlyTrendSeriesViewModel>();
        foreach (var subject in topSubjects)
        {
            var points = new List<MonthlyTrendPointViewModel>();
            for (int m = 5; m >= 0; m--)
            {
                var monthDate = new DateTime(now.Year, now.Month, 1).AddMonths(-m);
                var count = completed.Count(b => b.Subject == subject
                    && b.TutorAvailabilitySlot.StartTime.Year == monthDate.Year
                    && b.TutorAvailabilitySlot.StartTime.Month == monthDate.Month);
                points.Add(new MonthlyTrendPointViewModel { MonthLabel = monthDate.ToString("MMM"), CompletedSessions = count });
            }
            monthlyTrend.Add(new MonthlyTrendSeriesViewModel { Subject = subject, Points = points });
        }

        // Current streak: consecutive days with a completed session, only "live"
        // if the most recent completed day was today or yesterday.
        var completedDatesDesc = completed
            .Select(b => b.TutorAvailabilitySlot.StartTime.Date)
            .Distinct()
            .OrderByDescending(d => d)
            .ToList();

        int currentStreak = 0;
        if (completedDatesDesc.Any() && completedDatesDesc[0] >= today.AddDays(-1))
        {
            currentStreak = 1;
            for (int i = 1; i < completedDatesDesc.Count; i++)
            {
                if (completedDatesDesc[i - 1].AddDays(-1) == completedDatesDesc[i]) currentStreak++;
                else break;
            }
        }

        // Best streak ever - used for the achievement badge so it doesn't
        // "un-earn" itself once the live streak eventually breaks.
        var completedDatesAsc = completed
            .Select(b => b.TutorAvailabilitySlot.StartTime.Date)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        int bestStreak = 0, run = 0;
        DateTime? prevDate = null;
        foreach (var d in completedDatesAsc)
        {
            run = (prevDate.HasValue && prevDate.Value.AddDays(1) == d) ? run + 1 : 1;
            bestStreak = Math.Max(bestStreak, run);
            prevDate = d;
        }

        // Goals - real, student-entered
        var goals = await _context.Goals
            .Where(g => g.StudentProfileId == studentProfile.Id)
            .OrderBy(g => g.DueDate ?? DateTime.MaxValue)
            .ThenByDescending(g => g.CreatedAt)
            .ToListAsync();

        int goalCompletionPercent = goals.Any()
            ? (int)Math.Round(goals.Count(g => g.Status == "Completed") * 100.0 / goals.Count)
            : 0;

        // Achievements: check real conditions, award (idempotently, same
        // pattern as DbSeeder) the first time each one is genuinely met.
        var existingAchievementKeys = await _context.StudentAchievements
            .Where(a => a.StudentProfileId == studentProfile.Id)
            .Select(a => a.AchievementKey)
            .ToListAsync();

        var monthStart = new DateTime(now.Year, now.Month, 1);
        var studentMonthlyCounts = await _context.Bookings
            .Include(b => b.TutorAvailabilitySlot)
            .Where(b => b.Status == "Completed" && b.TutorAvailabilitySlot.StartTime >= monthStart)
            .GroupBy(b => b.StudentProfileId)
            .Select(g => new { StudentProfileId = g.Key, Count = g.Count() })
            .ToListAsync();

        bool isTopStudentThisMonth = false;
        if (studentMonthlyCounts.Any())
        {
            var maxCount = studentMonthlyCounts.Max(x => x.Count);
            isTopStudentThisMonth = maxCount > 0
                && studentMonthlyCounts.Where(x => x.Count == maxCount).Select(x => x.StudentProfileId).Contains(studentProfile.Id);
        }

        var achievementChecks = new (string Key, string Title, string Subtitle, string Icon, bool Met, string LockedProgress)[]
         {
            ("sessions_10", AchievementCatalog.Items["sessions_10"].Title, "Milestone", AchievementCatalog.Items["sessions_10"].Icon,
                completed.Count >= 10, completed.Count >= 10 ? "" : $"{10 - completed.Count} more session{(10 - completed.Count == 1 ? "" : "s")}"),
            ("streak_7", AchievementCatalog.Items["streak_7"].Title, "Consistency", AchievementCatalog.Items["streak_7"].Icon,
                bestStreak >= 7, bestStreak >= 7 ? "" : $"{7 - bestStreak} more day{(7 - bestStreak == 1 ? "" : "s")}"),
            ("top_student_month", AchievementCatalog.Items["top_student_month"].Title, now.ToString("MMMM yyyy"), AchievementCatalog.Items["top_student_month"].Icon,
                isTopStudentThisMonth, isTopStudentThisMonth ? "" : "Most completed sessions this month"),
            ("sessions_25", AchievementCatalog.Items["sessions_25"].Title, "Milestone", AchievementCatalog.Items["sessions_25"].Icon,
                completed.Count >= 25, completed.Count >= 25 ? "" : $"{25 - completed.Count} more session{(25 - completed.Count == 1 ? "" : "s")}"),
         };

        var newlyEarned = achievementChecks
            .Where(a => a.Met && !existingAchievementKeys.Contains(a.Key))
            .Select(a => new StudentAchievement { StudentProfileId = studentProfile.Id, AchievementKey = a.Key, UnlockedAt = DateTime.Now })
            .ToList();

        if (newlyEarned.Any())
        {
            _context.StudentAchievements.AddRange(newlyEarned);
            await _context.SaveChangesAsync();
        }

        var allAchievementRows = await _context.StudentAchievements
            .Where(a => a.StudentProfileId == studentProfile.Id)
            .ToListAsync();

        var achievementVms = achievementChecks.Select(a =>
        {
            var row = allAchievementRows.FirstOrDefault(r => r.AchievementKey == a.Key);
            return new AchievementViewModel
            {
                Key = a.Key,
                Title = a.Title,
                Subtitle = a.Subtitle,
                Icon = a.Icon,
                Unlocked = row != null,
                UnlockedAt = row?.UnlockedAt,
                LockedProgressText = row != null ? null : a.LockedProgress
            };
        }).ToList();

        var vm = new ProgressPageViewModel
        {
            TotalHoursLearned = Math.Round(totalHoursLearned, 1),
            HoursLearnedThisWeek = Math.Round(hoursThisWeek, 1),
            SessionsCompleted = completed.Count,
            SessionsCompletedThisMonth = completedThisMonth.Count,
            SubjectsActiveCount = subjectsActiveCount,
            CurrentStreakDays = currentStreak,
            GoalCompletionPercent = goalCompletionPercent,
            SubjectCards = subjectCards,
            WeeklyHours = weeklyHours,
            MonthlyTrend = monthlyTrend,
            Goals = goals.Select(g => new GoalRowViewModel
            {
                GoalId = g.Id,
                Subject = g.Subject,
                Description = g.Description,
                Status = g.Status,
                DueDate = g.DueDate
            }).ToList(),
            Achievements = achievementVms
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddGoal(string description, string? subject, DateTime? dueDate)
    {
        var studentProfile = await GetCurrentStudentProfileAsync();
        if (studentProfile == null) return RedirectToAction("StudentLogin", "Account");

        if (!string.IsNullOrWhiteSpace(description))
        {
            _context.Goals.Add(new Goal
            {
                StudentProfileId = studentProfile.Id,
                Subject = string.IsNullOrWhiteSpace(subject) ? null : subject.Trim(),
                Description = description.Trim(),
                Status = "NotStarted",
                DueDate = dueDate,
                CreatedAt = DateTime.Now
            });
            await _context.SaveChangesAsync();
        }

        return RedirectToAction("Progress");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateGoalStatus(int goalId, string status)
    {
        var studentProfile = await GetCurrentStudentProfileAsync();
        if (studentProfile == null) return RedirectToAction("StudentLogin", "Account");

        if (status is not ("NotStarted" or "InProgress" or "Completed")) return RedirectToAction("Progress");

        var goal = await _context.Goals
            .FirstOrDefaultAsync(g => g.Id == goalId && g.StudentProfileId == studentProfile.Id);

        if (goal != null)
        {
            goal.Status = status;
            await _context.SaveChangesAsync();
        }

        return RedirectToAction("Progress");
    }
    public async Task<IActionResult> Settings()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Index", "Home");

        var studentProfile = await _context.StudentProfiles.FirstOrDefaultAsync(s => s.UserId == user.Id);
        if (studentProfile == null) return RedirectToAction("Index", "Home");

        await SetSidebarContextAsync("settings");

        var vm = new SettingsPageViewModel
        {
            Initials = GetInitials(user.FullName),
            Profile = new SettingsProfileFormModel
            {
                FullName = user.FullName,
                Email = user.Email ?? "",
                PhoneNumber = user.PhoneNumber,
                GradeLevel = studentProfile.GradeLevel,
                District = user.District
            },
            Academic = new SettingsAcademicFormModel
            {
                SchoolName = studentProfile.SchoolName,
                CurriculumBoard = studentProfile.CurriculumBoard,
                SubjectsEnrolled = studentProfile.SubjectsEnrolled
            },
            Notifications = new SettingsNotificationsModel
            {
                SessionReminders = studentProfile.NotifySessionReminders,
                NewMessages = studentProfile.NotifyNewMessages,
                ProgressUpdates = studentProfile.NotifyProgressUpdates
            },
            ShowProfileToTutors = studentProfile.ShowProfileToTutors
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(SettingsProfileFormModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("StudentLogin", "Account");

        var studentProfile = await _context.StudentProfiles.FirstOrDefaultAsync(s => s.UserId == user.Id);
        if (studentProfile == null) return RedirectToAction("Index", "Home");

        if (string.IsNullOrWhiteSpace(model.FullName) || string.IsNullOrWhiteSpace(model.Email))
        {
            TempData["SettingsError"] = "Full name and email are required.";
            return RedirectToAction("Settings");
        }

        user.FullName = model.FullName.Trim();
        user.District = string.IsNullOrWhiteSpace(model.District) ? null : model.District.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim();

        if (!string.Equals(user.Email, model.Email, StringComparison.OrdinalIgnoreCase))
        {
            var setEmailResult = await _userManager.SetEmailAsync(user, model.Email.Trim());
            if (!setEmailResult.Succeeded)
            {
                TempData["SettingsError"] = "Could not update email: " + string.Join(" ", setEmailResult.Errors.Select(e => e.Description));
                return RedirectToAction("Settings");
            }
            await _userManager.SetUserNameAsync(user, model.Email.Trim());
        }

        studentProfile.GradeLevel = string.IsNullOrWhiteSpace(model.GradeLevel) ? null : model.GradeLevel.Trim();

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            TempData["SettingsError"] = "Could not save profile changes.";
            return RedirectToAction("Settings");
        }

        await _context.SaveChangesAsync();
        await _signInManager.RefreshSignInAsync(user);

        TempData["SettingsSuccess"] = "Profile updated.";
        return RedirectToAction("Settings");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAcademic(string? schoolName, string? curriculumBoard, string? subjectsEnrolled)
    {
        var studentProfile = await GetCurrentStudentProfileAsync();
        if (studentProfile == null) return RedirectToAction("StudentLogin", "Account");

        studentProfile.SchoolName = string.IsNullOrWhiteSpace(schoolName) ? null : schoolName.Trim();
        studentProfile.CurriculumBoard = string.IsNullOrWhiteSpace(curriculumBoard) ? null : curriculumBoard.Trim();
        studentProfile.SubjectsEnrolled = string.IsNullOrWhiteSpace(subjectsEnrolled) ? null : subjectsEnrolled.Trim(' ', ',');

        await _context.SaveChangesAsync();
        TempData["SettingsSuccess"] = "Academic details updated.";
        return RedirectToAction("Settings");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateNotificationPreference(string key, bool value)
    {
        var studentProfile = await GetCurrentStudentProfileAsync();
        if (studentProfile == null) return RedirectToAction("StudentLogin", "Account");

        switch (key)
        {
            case "SessionReminders": studentProfile.NotifySessionReminders = value; break;
            case "NewMessages": studentProfile.NotifyNewMessages = value; break;
            case "ProgressUpdates": studentProfile.NotifyProgressUpdates = value; break;
        }

        await _context.SaveChangesAsync();
        return RedirectToAction("Settings");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePrivacy(bool showProfileToTutors)
    {
        var studentProfile = await GetCurrentStudentProfileAsync();
        if (studentProfile == null) return RedirectToAction("StudentLogin", "Account");

        studentProfile.ShowProfileToTutors = showProfileToTutors;
        await _context.SaveChangesAsync();
        return RedirectToAction("Settings");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmNewPassword)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("StudentLogin", "Account");

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword != confirmNewPassword)
        {
            TempData["SettingsError"] = "New password and confirmation do not match.";
            return RedirectToAction("Settings");
        }

        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
        {
            TempData["SettingsError"] = string.Join(" ", result.Errors.Select(e => e.Description));
            return RedirectToAction("Settings");
        }

        await _signInManager.RefreshSignInAsync(user);
        TempData["SettingsSuccess"] = "Password changed.";
        return RedirectToAction("Settings");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAccount(string confirmText)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("StudentLogin", "Account");

        if (!string.Equals(confirmText?.Trim(), "DELETE", StringComparison.Ordinal))
        {
            TempData["SettingsError"] = "Type DELETE exactly to confirm account deletion.";
            return RedirectToAction("Settings");
        }

        var studentProfile = await _context.StudentProfiles.FirstOrDefaultAsync(s => s.UserId == user.Id);
        if (studentProfile == null) return RedirectToAction("Index", "Home");

        // Booking/Message/SavedTutor all use DeleteBehavior.Restrict against
        // StudentProfile, so they must be cleaned up manually before the
        // profile itself can be removed. Goal/StudentAchievement cascade
        // automatically (configured with DeleteBehavior.Cascade).
        using var transaction = await _context.Database.BeginTransactionAsync();

        var activeSlotIds = await _context.Bookings
            .Where(b => b.StudentProfileId == studentProfile.Id && b.Status != "Cancelled" && b.Status != "Completed")
            .Select(b => b.TutorAvailabilitySlotId)
            .ToListAsync();

        if (activeSlotIds.Any())
        {
            var slots = await _context.TutorAvailabilitySlots.Where(s => activeSlotIds.Contains(s.Id)).ToListAsync();
            foreach (var slot in slots) slot.IsBooked = false;
        }

        _context.Reviews.RemoveRange(_context.Reviews.Where(r => r.StudentProfileId == studentProfile.Id));
        _context.Messages.RemoveRange(_context.Messages.Where(m => m.StudentProfileId == studentProfile.Id)); _context.SavedTutors.RemoveRange(_context.SavedTutors.Where(s => s.StudentProfileId == studentProfile.Id));
        _context.Bookings.RemoveRange(_context.Bookings.Where(b => b.StudentProfileId == studentProfile.Id));
        _context.StudentProfiles.Remove(studentProfile);

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        await _signInManager.SignOutAsync();
        await _userManager.DeleteAsync(user);

        return RedirectToAction("Index", "Home");
    }

public async Task<IActionResult> HelpSupport()
    {
        var studentProfile = await GetCurrentStudentProfileAsync();
        if (studentProfile == null) return RedirectToAction("Index", "Home");

        await SetSidebarContextAsync("help");

        var tickets = await _context.SupportTickets
            .Where(t => t.StudentProfileId == studentProfile.Id)
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
    public async Task<IActionResult> SubmitSupportTicket(string category, string subject, string message)
    {
        var studentProfile = await GetCurrentStudentProfileAsync();
        if (studentProfile == null) return RedirectToAction("StudentLogin", "Account");

        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(message))
        {
            TempData["SettingsError"] = "Please fill in both a subject and a message.";
            return RedirectToAction("HelpSupport");
        }

        var validCategories = new[] { "Booking", "Messaging", "Account", "Other" };

        _context.SupportTickets.Add(new SupportTicket
        {
            StudentProfileId = studentProfile.Id,
            Category = validCategories.Contains(category) ? category : "Other",
            Subject = subject.Trim(),
            Message = message.Trim(),
            Status = "Open",
            CreatedAt = DateTime.Now
        });

        await _context.SaveChangesAsync();

        TempData["SettingsSuccess"] = "Your request has been submitted. We'll get back to you by email.";
        return RedirectToAction("HelpSupport");
    }
}