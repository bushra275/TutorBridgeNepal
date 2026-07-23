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
            booking.TutorAvailabilitySlot.IsBooked = false;
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
}