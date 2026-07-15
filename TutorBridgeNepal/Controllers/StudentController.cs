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

        ViewData["SidebarName"] = user.FullName;
        ViewData["SidebarInitials"] = GetInitials(user.FullName);
        ViewData["SidebarMeta"] = string.IsNullOrWhiteSpace(subMeta) ? "Student" : subMeta;
        ViewData["ActiveNav"] = activeNav;

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
            SubjectProgress = subjectProgress
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
                IsVerified = t.IsVerified
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
            .Take(12)
            .ToListAsync();

        var vm = new TutorProfileDetailViewModel
        {
            TutorProfileId = tutor.Id,
            FullName = tutor.User.FullName,
            Initials = GetInitials(tutor.User.FullName),
            Subjects = tutor.Subjects,
            District = tutor.User.District,
            Bio = tutor.Bio,
            YearsOfExperience = tutor.YearsOfExperience,
            AverageRating = tutor.AverageRating,
            IsVerified = tutor.IsVerified,
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
                Status = b.Status
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
}