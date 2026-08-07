using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutorBridgeNepal.Data;
using TutorBridgeNepal.Models;
using TutorBridgeNepal.ViewModels;

namespace TutorBridgeNepal.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
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
        var admin = await _userManager.GetUserAsync(User);
        var now = DateTime.Now;
        var monthStart = new DateTime(now.Year, now.Month, 1);

        var totalUsers = await _context.Users.CountAsync();
        var usersBeforeThisMonth = await _context.Users.CountAsync(u => u.CreatedAt < monthStart);
        int? totalUsersTrend = usersBeforeThisMonth == 0 ? null : (int)Math.Round(100.0 * (totalUsers - usersBeforeThisMonth) / usersBeforeThisMonth);

        var activeTutors = await _context.TutorProfiles
            .CountAsync(t => t.IsVerified && !t.IsDeactivated);

        // Approximation: tutor accounts (of any verification status) that
        // already existed before this month, as a growth baseline. There's no
        // historical snapshot of verification status itself, so this reflects
        // tutor sign-up growth rather than verified-tutor growth specifically.
        var tutorsBeforeThisMonth = await _context.TutorProfiles
            .CountAsync(t => t.User.CreatedAt < monthStart);
        var totalTutors = await _context.TutorProfiles.CountAsync();
        int? activeTutorsTrend = tutorsBeforeThisMonth == 0 ? null : (int)Math.Round(100.0 * (totalTutors - tutorsBeforeThisMonth) / tutorsBeforeThisMonth);

        var previousMonthStart = monthStart.AddMonths(-1);
        var sessionsThisMonth = await _context.Bookings
            .Include(b => b.TutorAvailabilitySlot)
            .Where(b => b.Status != "Cancelled" && b.TutorAvailabilitySlot.StartTime >= monthStart)
            .CountAsync();
        var sessionsPreviousMonth = await _context.Bookings
            .Include(b => b.TutorAvailabilitySlot)
            .Where(b => b.Status != "Cancelled"
                && b.TutorAvailabilitySlot.StartTime >= previousMonthStart
                && b.TutorAvailabilitySlot.StartTime < monthStart)
            .CountAsync();
        int? sessionsTrend = sessionsPreviousMonth == 0 ? null : (int)Math.Round(100.0 * (sessionsThisMonth - sessionsPreviousMonth) / sessionsPreviousMonth);

        var pendingTutors = await _context.TutorProfiles
            .Include(t => t.User)
            .Where(t => !t.IsVerified && !t.VerificationRejected)
            .OrderBy(t => t.User.CreatedAt)
            .ToListAsync();

        var openTickets = await _context.SupportTickets
            .Where(t => t.Status == "Open")
            .CountAsync();

        // Session activity - current month so far, day 1 through today.
        var chartStart = monthStart;
        var chartEnd = now.Date;
        var recentBookings = await _context.Bookings
            .Include(b => b.TutorAvailabilitySlot)
            .Where(b => b.Status != "Cancelled"
                && b.TutorAvailabilitySlot.StartTime >= chartStart
                && b.TutorAvailabilitySlot.StartTime < chartEnd.AddDays(1))
            .Select(b => b.TutorAvailabilitySlot.StartTime.Date)
            .ToListAsync();

        var chartLabels = new List<string>();
        var chartValues = new List<int>();
        for (var d = chartStart; d <= chartEnd; d = d.AddDays(1))
        {
            chartLabels.Add(d.Day.ToString());
            chartValues.Add(recentBookings.Count(x => x == d));
        }

        // Recent sessions - most recently created bookings, any status.
        var recentSessionRows = await _context.Bookings
            .Include(b => b.StudentProfile).ThenInclude(s => s.User)
            .Include(b => b.TutorProfile).ThenInclude(t => t.User)
            .Include(b => b.TutorAvailabilitySlot)
            .OrderByDescending(b => b.CreatedAt)
            .Take(6)
            .Select(b => new AdminSessionRowViewModel
            {
                StudentName = b.StudentProfile.User.FullName,
                TutorName = b.TutorProfile.User.FullName,
                Subject = b.Subject,
                Date = b.TutorAvailabilitySlot.StartTime,
                Status = b.Status
            })
            .ToListAsync();

        // Open complaints - both student- and tutor-filed support tickets.
        var openComplaintTickets = await _context.SupportTickets
            .Include(t => t.StudentProfile).ThenInclude(s => s!.User)
            .Include(t => t.TutorProfile).ThenInclude(t => t!.User)
            .Where(t => t.Status == "Open")
            .OrderByDescending(t => t.CreatedAt)
            .Take(4)
            .ToListAsync();

        var openComplaintRows = openComplaintTickets.Select(t => new AdminComplaintRowViewModel
        {
            SupportTicketId = t.Id,
            Subject = t.Subject,
            ReporterName = t.StudentProfileId != null ? t.StudentProfile!.User.FullName : t.TutorProfile!.User.FullName,
            ReporterRole = t.StudentProfileId != null ? "Student" : "Tutor",
            CreatedAt = t.CreatedAt
        }).ToList();

        var pendingVerificationRows = pendingTutors.Take(3).Select(t => new AdminVerificationRowViewModel
        {
            TutorProfileId = t.Id,
            Name = t.User.FullName,
            Initials = GetInitials(t.User.FullName),
            FirstSubject = t.Subjects.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault(),
            District = t.User.District
        }).ToList();

        // Recent registrations - newest users across both roles.
        var recentStudents = await _context.StudentProfiles
            .Include(s => s.User)
            .OrderByDescending(s => s.User.CreatedAt)
            .Take(6)
            .Select(s => new AdminUserRowViewModel
            {
                Name = s.User.FullName,
                Role = "Student",
                District = s.User.District,
                StatusLabel = "Active"
            })
            .ToListAsync();

        var recentTutors = await _context.TutorProfiles
            .Include(t => t.User)
            .OrderByDescending(t => t.User.CreatedAt)
            .Take(6)
            .Select(t => new AdminUserRowViewModel
            {
                Name = t.User.FullName,
                Role = "Tutor",
                District = t.User.District,
                StatusLabel = t.IsVerified ? "Active" : "Pending"
            })
            .ToListAsync();

        var recentRegistrations = recentStudents.Concat(recentTutors)
            .Take(6)
            .ToList();

        // Platform health - all computed from real data.
        var totalTutorDecisions = await _context.TutorProfiles.CountAsync(t => t.IsVerified || t.VerificationRejected);
        var approvedTutorDecisions = await _context.TutorProfiles.CountAsync(t => t.IsVerified);
        var tutorApprovalRate = totalTutorDecisions == 0 ? 0 : (int)Math.Round(100.0 * approvedTutorDecisions / totalTutorDecisions);

        var totalDecidedSessions = await _context.Bookings.CountAsync(b => b.Status == "Completed" || b.Status == "Missed");
        var completedSessions = await _context.Bookings.CountAsync(b => b.Status == "Completed");
        var sessionCompletionRate = totalDecidedSessions == 0 ? 0 : (int)Math.Round(100.0 * completedSessions / totalDecidedSessions);

        var allRatings = await _context.Reviews.Select(r => r.Rating).ToListAsync();
        var studentSatisfaction = allRatings.Count == 0 ? 0 : (int)Math.Round(20.0 * allRatings.Average());

        var totalTickets = await _context.SupportTickets.CountAsync();
        var resolvedTickets = await _context.SupportTickets.CountAsync(t => t.Status == "Resolved");
        var complaintResolutionRate = totalTickets == 0 ? 0 : (int)Math.Round(100.0 * resolvedTickets / totalTickets);

        // Recent activity log - interleave a handful of real events across the platform.
        var activity = new List<AdminActivityItemViewModel>();

        var lastRegistrations = await _context.Users
            .OrderByDescending(u => u.CreatedAt)
            .Take(3)
            .ToListAsync();
        foreach (var u in lastRegistrations)
        {
            activity.Add(new AdminActivityItemViewModel
            {
                Timestamp = u.CreatedAt,
                BoldLead = "New registration",
                RestText = $"{u.FullName}{(string.IsNullOrWhiteSpace(u.District) ? "" : $", {u.District}")}",
                Tag = "User",
                DotClass = "green",
                TagClass = "student"
            });
        }

        var lastCompletedSessions = await _context.Bookings
            .Include(b => b.StudentProfile).ThenInclude(s => s.User)
            .Include(b => b.TutorProfile).ThenInclude(t => t.User)
            .Where(b => b.Status == "Completed")
            .OrderByDescending(b => b.CreatedAt)
            .Take(3)
            .ToListAsync();
        foreach (var b in lastCompletedSessions)
        {
            activity.Add(new AdminActivityItemViewModel
            {
                Timestamp = b.CreatedAt,
                BoldLead = "Session completed",
                RestText = $"{b.TutorProfile.User.FullName} \u00d7 {b.StudentProfile.User.FullName} ({b.Subject})",
                Tag = "Session",
                DotClass = "green",
                TagClass = "session"
            });
        }

        var lastPendingTutors = pendingTutors.OrderByDescending(t => t.User.CreatedAt).Take(3);
        foreach (var t in lastPendingTutors)
        {
            activity.Add(new AdminActivityItemViewModel
            {
                Timestamp = t.User.CreatedAt,
                BoldLead = "Tutor verification submitted",
                RestText = $"{t.User.FullName}, {t.Subjects.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault()}",
                Tag = "Verify",
                DotClass = "orange",
                TagClass = "verify"
            });
        }

        foreach (var t in openComplaintTickets.Take(3))
        {
            var reporterName = t.StudentProfileId != null ? t.StudentProfile!.User.FullName : t.TutorProfile!.User.FullName;
            activity.Add(new AdminActivityItemViewModel
            {
                Timestamp = t.CreatedAt,
                BoldLead = "Support request filed",
                RestText = $"{reporterName} - {t.Subject}",
                Tag = "Review",
                DotClass = "red",
                TagClass = "review"
            });
        }

        var vm = new AdminDashboardViewModel
        {
            AdminName = admin?.FullName ?? "Administrator",
            AdminInitials = GetInitials(admin?.FullName ?? "Administrator"),
            TodayLabel = now.ToString("dddd, d MMMM yyyy"),
            TotalUsers = totalUsers,
            ActiveTutors = activeTutors,
            SessionsThisMonth = sessionsThisMonth,
            TotalUsersTrendPercent = totalUsersTrend,
            ActiveTutorsTrendPercent = activeTutorsTrend,
            SessionsTrendPercent = sessionsTrend,
            ChartMonthLabel = now.ToString("MMMM yyyy"),
            PendingVerificationsCount = pendingTutors.Count,
            OpenComplaintsCount = openTickets,
            ChartLabels = chartLabels,
            ChartValues = chartValues,
            RecentSessions = recentSessionRows,
            OpenComplaints = openComplaintRows,
            PendingVerifications = pendingVerificationRows,
            RecentRegistrations = recentRegistrations,
            RecentActivity = activity.OrderByDescending(a => a.Timestamp).Take(6).ToList(),
            TutorApprovalRatePercent = tutorApprovalRate,
            SessionCompletionRatePercent = sessionCompletionRate,
            StudentSatisfactionPercent = studentSatisfaction,
            ComplaintResolutionRatePercent = complaintResolutionRate
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveTutor(int tutorProfileId)
    {
        var tutor = await _context.TutorProfiles.FirstOrDefaultAsync(t => t.Id == tutorProfileId);
        if (tutor != null)
        {
            tutor.IsVerified = true;
            tutor.VerificationRejected = false;
            await _context.SaveChangesAsync();
        }

        return RedirectToAction("Dashboard");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectTutor(int tutorProfileId)
    {
        var tutor = await _context.TutorProfiles.FirstOrDefaultAsync(t => t.Id == tutorProfileId);
        if (tutor != null)
        {
            tutor.IsVerified = false;
            tutor.VerificationRejected = true;
            await _context.SaveChangesAsync();
        }

        return RedirectToAction("Dashboard");
    }
}