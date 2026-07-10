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

    public StudentController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
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
                HourlyRate = t.HourlyRate,
                AverageRating = t.AverageRating
            }).ToList(),
            SubjectProgress = subjectProgress
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelBooking(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("StudentLogin", "Account");

        var studentProfile = await _context.StudentProfiles
            .FirstOrDefaultAsync(s => s.UserId == user.Id);
        if (studentProfile == null) return RedirectToAction("Dashboard");

        var booking = await _context.Bookings
            .Include(b => b.TutorAvailabilitySlot)
            .FirstOrDefaultAsync(b => b.Id == id && b.StudentProfileId == studentProfile.Id);

        if (booking != null && booking.Status != "Completed" && booking.Status != "Cancelled")
        {
            booking.Status = "Cancelled";
            booking.TutorAvailabilitySlot.IsBooked = false;
            await _context.SaveChangesAsync();
        }

        return RedirectToAction("Dashboard");
    }
}