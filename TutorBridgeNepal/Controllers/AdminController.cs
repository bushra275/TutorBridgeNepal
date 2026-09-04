using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutorBridgeNepal.Data;
using TutorBridgeNepal.Helpers;
using TutorBridgeNepal.Models;
using TutorBridgeNepal.ViewModels;

namespace TutorBridgeNepal.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly TutorBridgeNepal.Services.IEmailSender _emailSender;

    public AdminController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, IWebHostEnvironment webHostEnvironment, TutorBridgeNepal.Services.IEmailSender emailSender)
    {
        _context = context;
        _userManager = userManager;
        _signInManager = signInManager;
        _webHostEnvironment = webHostEnvironment;
        _emailSender = emailSender;
    }

    // Best-effort - a failed email should never block an approve/reject/
    // request-info/schedule-interview action, which has already been saved
    // to the database by the time this runs.
    private async Task SendTutorVerificationEmailAsync(ApplicationUser user, string subject, string bodyHtml)
    {
        if (string.IsNullOrWhiteSpace(user.Email)) return;

        var sender = _emailSender as TutorBridgeNepal.Services.SmtpEmailSender;
        if (sender == null || !sender.IsConfigured) return;

        try
        {
            await _emailSender.SendEmailAsync(user.Email, subject, bodyHtml);
        }
        catch
        {
            // Logged inside SmtpEmailSender already - nothing more to do here.
        }
    }

    // Returns the single PlatformSettings row, creating it with defaults
    // the first time it's needed (belt-and-braces alongside the row the
    // AddPlatformSettings migration seeds).
    private async Task<PlatformSettings> GetOrCreatePlatformSettingsAsync()
    {
        var settings = await _context.PlatformSettings.FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new PlatformSettings();
            _context.PlatformSettings.Add(settings);
            await _context.SaveChangesAsync();
        }
        return settings;
    }

    // Hard-deletes a user account and everything that references it. Mirrors
    // StudentController.DeleteAccount's transaction pattern (that page only
    // has to handle the student side); this handles both roles, since an
    // admin can delete either kind of account from User Management.
    private async Task DeleteUserAccountAsync(ApplicationUser user, string role)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();

        if (role == "Student")
        {
            var student = await _context.StudentProfiles.FirstOrDefaultAsync(s => s.UserId == user.Id);
            if (student != null)
            {
                var activeSlotIds = await _context.Bookings
                    .Where(b => b.StudentProfileId == student.Id
                        && b.Status != "Cancelled" && b.Status != "Completed" && b.Status != "Missed")
                    .Select(b => b.TutorAvailabilitySlotId)
                    .Distinct()
                    .ToListAsync();

                if (activeSlotIds.Any())
                {
                    var slots = await _context.TutorAvailabilitySlots.Where(s => activeSlotIds.Contains(s.Id)).ToListAsync();
                    foreach (var slot in slots)
                    {
                        var remainingActive = await _context.Bookings.CountAsync(b =>
                            b.TutorAvailabilitySlotId == slot.Id
                            && b.StudentProfileId != student.Id
                            && b.Status != "Cancelled");
                        slot.IsBooked = remainingActive >= slot.Capacity;
                    }
                }

                _context.Reviews.RemoveRange(_context.Reviews.Where(r => r.StudentProfileId == student.Id));
                _context.Messages.RemoveRange(_context.Messages.Where(m => m.StudentProfileId == student.Id));
                _context.SavedTutors.RemoveRange(_context.SavedTutors.Where(s => s.StudentProfileId == student.Id));
                _context.Bookings.RemoveRange(_context.Bookings.Where(b => b.StudentProfileId == student.Id));
                // Goals, StudentAchievements, SupportTickets (student side) all
                // cascade automatically once StudentProfile is removed below.
                _context.StudentProfiles.Remove(student);
            }
        }
        else if (role == "Tutor")
        {
            var tutor = await _context.TutorProfiles.FirstOrDefaultAsync(t => t.UserId == user.Id);
            if (tutor != null)
            {
                var activeSlotIds = await _context.Bookings
                    .Where(b => b.TutorProfileId == tutor.Id
                        && b.Status != "Cancelled" && b.Status != "Completed" && b.Status != "Missed")
                    .Select(b => b.TutorAvailabilitySlotId)
                    .Distinct()
                    .ToListAsync();

                if (activeSlotIds.Any())
                {
                    var slots = await _context.TutorAvailabilitySlots.Where(s => activeSlotIds.Contains(s.Id)).ToListAsync();
                    foreach (var slot in slots)
                    {
                        var remainingActive = await _context.Bookings.CountAsync(b =>
                            b.TutorAvailabilitySlotId == slot.Id
                            && b.TutorProfileId != tutor.Id
                            && b.Status != "Cancelled");
                        slot.IsBooked = remainingActive >= slot.Capacity;
                    }
                }

                // Order matters: Review -> Booking -> TutorAvailabilitySlot, since
                // Review restricts on Booking, and Booking restricts on Slot.
                _context.Reviews.RemoveRange(_context.Reviews.Where(r => r.TutorProfileId == tutor.Id));
                _context.Messages.RemoveRange(_context.Messages.Where(m => m.TutorProfileId == tutor.Id));
                _context.SavedTutors.RemoveRange(_context.SavedTutors.Where(s => s.TutorProfileId == tutor.Id));
                _context.SupportTickets.RemoveRange(_context.SupportTickets.Where(t => t.TutorProfileId == tutor.Id));
                _context.Bookings.RemoveRange(_context.Bookings.Where(b => b.TutorProfileId == tutor.Id));
                _context.TutorAvailabilitySlots.RemoveRange(_context.TutorAvailabilitySlots.Where(s => s.TutorProfileId == tutor.Id));
                _context.TutorCredentials.RemoveRange(_context.TutorCredentials.Where(c => c.TutorProfileId == tutor.Id));
                _context.TutorSubjects.RemoveRange(_context.TutorSubjects.Where(s => s.TutorProfileId == tutor.Id));
                // TutorWeeklyAvailabilityRules, TutorTimeOffs, TutorCalendarConnections
                // all cascade automatically once TutorProfile is removed below.
                _context.TutorProfiles.Remove(tutor);

                // Verification documents live outside wwwroot in App_Data, keyed
                // by tutor profile id - the whole folder goes with the account.
                var docsFolder = Path.Combine(_webHostEnvironment.ContentRootPath, "App_Data", "tutor-documents", tutor.Id.ToString());
                if (Directory.Exists(docsFolder))
                {
                    try { Directory.Delete(docsFolder, recursive: true); } catch { /* non-fatal cleanup */ }
                }
            }
        }

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        FileUploadHelper.TryDelete(user.PhotoUrl, _webHostEnvironment.WebRootPath);
        // UserDevices cascade-delete automatically via the FK to AspNetUsers.
        await _userManager.DeleteAsync(user);
    }

    // Populates ViewData with the unread-notification bell preview
    // (icon/title/subtitle for the latest few unread notifications, plus
    // the total unread count) so the shared admin layout can render the
    // notification bell dropdown without every action having to build it
    // by hand. Call this from any action whose view uses the admin layout.
    private async Task SetAdminNotificationBellAsync()
    {
        var admin = await _userManager.GetUserAsync(User);
        ViewData["AdminProfileName"] = admin?.FullName ?? "Administrator";
        ViewData["AdminProfileInitials"] = GetInitials(admin?.FullName ?? "Administrator");
        ViewData["AdminProfilePhotoUrl"] = admin?.PhotoUrl;

        var unreadCount = await _context.Notifications.CountAsync(n => !n.IsRead);
        var preview = await _context.Notifications
            .Where(n => !n.IsRead)
            .OrderByDescending(n => n.CreatedAt)
            .Take(6)
            .Select(n => new AdminNotifBellItemViewModel
            {
                Icon = n.Icon,
                Title = n.Title,
                Subtitle = n.Message.Length > 70 ? n.Message.Substring(0, 70) + "…" : n.Message
            })
            .ToListAsync();

        ViewData["AdminNotifications"] = preview;
        ViewData["AdminNotificationCount"] = unreadCount;

        ViewData["SidebarPendingVerificationsCount"] = await _context.TutorProfiles
            .CountAsync(t => !t.IsVerified && !t.VerificationRejected);

        ViewData["SidebarOpenComplaintsCount"] = await _context.SupportTickets
            .CountAsync(t => t.Status != "Resolved");
    }

    // ── 5a: Helper methods ────────────────────────────────────────────────

    private static string GetInitials(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "?";
        if (parts.Length == 1) return parts[0][..Math.Min(2, parts[0].Length)].ToUpper();
        return $"{parts[0][0]}{parts[^1][0]}".ToUpper();
    }

    // Privacy-conscious display of a phone number on the verification queue,
    // e.g. "9812345678" -> "98XXXXXX78". Short/unset numbers are returned
    // as-is (nothing meaningful left to mask) or as null.
    private static string? MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var digits = phone.Trim();
        if (digits.Length <= 4) return digits;

        var visibleStart = digits[..2];
        var visibleEnd = digits[^2..];
        var maskedLength = digits.Length - 4;
        return visibleStart + new string('X', maskedLength) + visibleEnd;
    }

    // The four documents the verification checklist always looks for. Kept
    // as a single source of truth so the query, the "missing" check and the
    // view all agree on labels/icons.
    private static readonly (string Type, string Label, string Icon)[] RequiredVerificationDocuments =
        {
        ("Citizenship", "Citizenship", "🪪"),
        ("CVResume", "CV / Resume", "📄"),
        ("DegreeCertificate", "Degree Certificate", "🎓"),
        ("PoliceReport", "Police Report", "🛡️"),
    };

    // ── Dashboard ─────────────────────────────────────────────────────────

    public async Task<IActionResult> Dashboard()
    {
        await SetAdminNotificationBellAsync();
        var admin = await _userManager.GetUserAsync(User);
        var now = DateTime.Now;
        var monthStart = new DateTime(now.Year, now.Month, 1);

        var totalUsers = await _context.Users.CountAsync();
        var usersBeforeThisMonth = await _context.Users.CountAsync(u => u.CreatedAt < monthStart);
        int? totalUsersTrend = usersBeforeThisMonth == 0 ? null : (int)Math.Round(100.0 * (totalUsers - usersBeforeThisMonth) / usersBeforeThisMonth);

        var activeTutors = await _context.TutorProfiles
            .CountAsync(t => t.IsVerified && !t.IsDeactivated);

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
                    .Where(t => t.Status != "Resolved")
                    .CountAsync();

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

        var totalTutorDecisions = await _context.TutorProfiles.CountAsync(t => t.IsVerified || t.VerificationRejected);
        var approvedTutorDecisions = await _context.TutorProfiles.CountAsync(t => t.IsVerified);
        var tutorApprovalRate = totalTutorDecisions == 0 ? 0 : (int)Math.Round(100.0 * approvedTutorDecisions / totalTutorDecisions);

        var totalDecidedSessions = await _context.Bookings.CountAsync(b => b.Status == "Completed" || b.Status == "Missed");
        var completedSessions = await _context.Bookings.CountAsync(b => b.Status == "Completed");
        var sessionCompletionRate = totalDecidedSessions == 0 ? 0 : (int)Math.Round(100.0 * completedSessions / totalDecidedSessions);

        var allRatings = await _context.Reviews.Select(r => r.Rating).ToListAsync();
        var studentSatisfaction = allRatings.Count == 0 ? 0 : (int)Math.Round(100.0 * allRatings.Count(r => r >= 4) / allRatings.Count);

        var totalTickets = await _context.SupportTickets.CountAsync();
        var resolvedTickets = await _context.SupportTickets.CountAsync(t => t.Status == "Resolved");
        var complaintResolutionRate = totalTickets == 0 ? 0 : (int)Math.Round(100.0 * resolvedTickets / totalTickets);

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

    // Exports a snapshot of what's currently on the Admin Dashboard: the
    // top KPI strip, the platform health rates, and the most recent
    // sessions/registrations tables - recomputed independently (same
    // pattern as the other Export*Csv actions) rather than reusing the
    // Dashboard() view model.
    public async Task<IActionResult> ExportDashboardReportCsv()
    {
        var now = DateTime.Now;
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var previousMonthStart = monthStart.AddMonths(-1);

        var totalUsers = await _context.Users.CountAsync();
        var usersBeforeThisMonth = await _context.Users.CountAsync(u => u.CreatedAt < monthStart);
        int? totalUsersTrend = usersBeforeThisMonth == 0 ? null : (int)Math.Round(100.0 * (totalUsers - usersBeforeThisMonth) / usersBeforeThisMonth);

        var activeTutors = await _context.TutorProfiles.CountAsync(t => t.IsVerified && !t.IsDeactivated);
        var tutorsBeforeThisMonth = await _context.TutorProfiles.CountAsync(t => t.User.CreatedAt < monthStart);
        var totalTutors = await _context.TutorProfiles.CountAsync();
        int? activeTutorsTrend = tutorsBeforeThisMonth == 0 ? null : (int)Math.Round(100.0 * (totalTutors - tutorsBeforeThisMonth) / tutorsBeforeThisMonth);

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

        var pendingVerifications = await _context.TutorProfiles.CountAsync(t => !t.IsVerified && !t.VerificationRejected);
        var openComplaints = await _context.SupportTickets.CountAsync(t => t.Status != "Resolved");

        var totalTutorDecisions = await _context.TutorProfiles.CountAsync(t => t.IsVerified || t.VerificationRejected);
        var approvedTutorDecisions = await _context.TutorProfiles.CountAsync(t => t.IsVerified);
        var tutorApprovalRate = totalTutorDecisions == 0 ? 0 : (int)Math.Round(100.0 * approvedTutorDecisions / totalTutorDecisions);

        var totalDecidedSessions = await _context.Bookings.CountAsync(b => b.Status == "Completed" || b.Status == "Missed");
        var completedSessions = await _context.Bookings.CountAsync(b => b.Status == "Completed");
        var sessionCompletionRate = totalDecidedSessions == 0 ? 0 : (int)Math.Round(100.0 * completedSessions / totalDecidedSessions);

        var allRatings = await _context.Reviews.Select(r => r.Rating).ToListAsync();
        var studentSatisfaction = allRatings.Count == 0 ? 0 : (int)Math.Round(100.0 * allRatings.Count(r => r >= 4) / allRatings.Count);

        var totalTickets = await _context.SupportTickets.CountAsync();
        var resolvedTickets = await _context.SupportTickets.CountAsync(t => t.Status == "Resolved");
        var complaintResolutionRate = totalTickets == 0 ? 0 : (int)Math.Round(100.0 * resolvedTickets / totalTickets);

        var recentSessions = await _context.Bookings
            .Include(b => b.StudentProfile).ThenInclude(s => s.User)
            .Include(b => b.TutorProfile).ThenInclude(t => t.User)
            .Include(b => b.TutorAvailabilitySlot)
            .OrderByDescending(b => b.CreatedAt)
            .Take(10)
            .ToListAsync();

        var recentUsers = await _context.Users
            .OrderByDescending(u => u.CreatedAt)
            .Take(10)
            .ToListAsync();

        string Esc(string? v) => "\"" + (v ?? "").Replace("\"", "\"\"") + "\"";
        string Pct(int? v) => v.HasValue ? $"{v}%" : "N/A";

        var csv = new System.Text.StringBuilder();
        csv.AppendLine($"TutorBridge Nepal - Admin Dashboard Report,{now:yyyy-MM-dd HH:mm}");
        csv.AppendLine();

        csv.AppendLine("KPI,Value,TrendVsLastMonth");
        csv.AppendLine($"Total users,{totalUsers},{Pct(totalUsersTrend)}");
        csv.AppendLine($"Active tutors,{activeTutors},{Pct(activeTutorsTrend)}");
        csv.AppendLine($"Sessions this month,{sessionsThisMonth},{Pct(sessionsTrend)}");
        csv.AppendLine($"Pending tutor verifications,{pendingVerifications},");
        csv.AppendLine($"Open complaints,{openComplaints},");
        csv.AppendLine();

        csv.AppendLine("Platform health,Rate");
        csv.AppendLine($"Tutor approval rate,{tutorApprovalRate}%");
        csv.AppendLine($"Session completion rate,{sessionCompletionRate}%");
        csv.AppendLine($"Student satisfaction,{studentSatisfaction}%");
        csv.AppendLine($"Complaint resolution rate,{complaintResolutionRate}%");
        csv.AppendLine();

        csv.AppendLine("Recent sessions");
        csv.AppendLine("Student,Tutor,Subject,Date,Status");
        foreach (var b in recentSessions)
        {
            csv.AppendLine(string.Join(",",
                Esc(b.StudentProfile.User.FullName), Esc(b.TutorProfile.User.FullName), Esc(b.Subject),
                Esc(b.TutorAvailabilitySlot.StartTime.ToString("yyyy-MM-dd HH:mm")), Esc(b.Status)));
        }
        csv.AppendLine();

        csv.AppendLine("Recent registrations");
        csv.AppendLine("Name,Role,District,Joined");
        foreach (var u in recentUsers)
        {
            var isTutor = await _context.TutorProfiles.AnyAsync(t => t.UserId == u.Id);
            var role = isTutor ? "Tutor" : "Student";
            csv.AppendLine(string.Join(",", Esc(u.FullName), Esc(role), Esc(u.District), Esc(u.CreatedAt.ToString("yyyy-MM-dd"))));
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
        return File(bytes, "text/csv", $"tutorbridge-dashboard-report-{DateTime.Now:yyyyMMdd-HHmm}.csv");
    }

    // ── User Management ───────────────────────────────────────────────────

    public async Task<IActionResult> UserManagement(
        string tab = "all",
        string? search = null,
        string? role = null,
        string? district = null,
        string? status = null,
        string registered = "all",
        string sort = "name_asc",
        int page = 1,
        int pageSize = 8)
    {
        await SetAdminNotificationBellAsync();
        var admin = await _userManager.GetUserAsync(User);
        var now = DateTime.Now;
        var monthStart = new DateTime(now.Year, now.Month, 1);

        var totalUsers = await _context.Users.CountAsync();
        var totalStudents = await _context.StudentProfiles.CountAsync();
        var totalTutors = await _context.TutorProfiles.CountAsync();
        var pendingApproval = await _context.TutorProfiles.CountAsync(t => !t.IsVerified && !t.VerificationRejected);
        var suspendedCount = await _context.Users.CountAsync(u => u.IsSuspended);

        var usersBeforeThisMonth = await _context.Users.CountAsync(u => u.CreatedAt < monthStart);
        int? totalUsersTrend = usersBeforeThisMonth == 0 ? null : (int)Math.Round(100.0 * (totalUsers - usersBeforeThisMonth) / usersBeforeThisMonth);

        var studentsBeforeThisMonth = await _context.StudentProfiles.CountAsync(s => s.User.CreatedAt < monthStart);
        int? studentsTrend = studentsBeforeThisMonth == 0 ? null : (int)Math.Round(100.0 * (totalStudents - studentsBeforeThisMonth) / studentsBeforeThisMonth);

        var tutorsBeforeThisMonth = await _context.TutorProfiles.CountAsync(t => t.User.CreatedAt < monthStart);
        int? tutorsTrend = tutorsBeforeThisMonth == 0 ? null : (int)Math.Round(100.0 * (totalTutors - tutorsBeforeThisMonth) / tutorsBeforeThisMonth);

        var districts = await _context.Users
            .Where(u => u.District != null && u.District != "")
            .Select(u => u.District!)
            .Distinct()
            .OrderBy(d => d)
            .ToListAsync();

        var completedByStudent = await _context.Bookings
            .Where(b => b.Status == "Completed")
            .GroupBy(b => b.StudentProfileId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        var completedByTutor = await _context.Bookings
            .Where(b => b.Status == "Completed")
            .GroupBy(b => b.TutorProfileId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        var studentProfiles = await _context.StudentProfiles.Include(s => s.User).ToListAsync();
        var tutorProfiles = await _context.TutorProfiles.Include(t => t.User).ToListAsync();

        var rows = new List<AdminUserRowFullViewModel>();

        rows.AddRange(studentProfiles.Select(s => new AdminUserRowFullViewModel
        {
            UserId = s.UserId,
            Name = s.User.FullName,
            Initials = GetInitials(s.User.FullName),
            Email = s.User.Email ?? "",
            Role = "Student",
            IdCode = $"STU-{s.Id:D4}",
            SubLabel = string.IsNullOrWhiteSpace(s.GradeLevel) ? null : s.GradeLevel,
            District = s.User.District,
            JoinedAt = s.User.CreatedAt,
            SessionCount = completedByStudent.TryGetValue(s.Id, out var sc) ? sc : 0,
            Status = s.User.IsSuspended ? "Suspended" : "Active",
            TutorProfileId = null
        }));

        rows.AddRange(tutorProfiles.Select(t => new AdminUserRowFullViewModel
        {
            UserId = t.UserId,
            Name = t.User.FullName,
            Initials = GetInitials(t.User.FullName),
            Email = t.User.Email ?? "",
            Role = "Tutor",
            IdCode = $"TUT-{t.Id:D4}",
            SubLabel = string.IsNullOrWhiteSpace(t.Subjects)
                ? null
                : string.Join(", ", t.Subjects.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(2)),
            District = t.User.District,
            JoinedAt = t.User.CreatedAt,
            SessionCount = completedByTutor.TryGetValue(t.Id, out var tc) ? tc : 0,
            Status = t.User.IsSuspended
                ? "Suspended"
                : t.VerificationRejected
                    ? "Rejected"
                    : !t.IsVerified
                        ? "Pending"
                        : "Active",
            TutorProfileId = t.Id
        }));

        IEnumerable<AdminUserRowFullViewModel> filtered = tab switch
        {
            "students" => rows.Where(r => r.Role == "Student"),
            "tutors" => rows.Where(r => r.Role == "Tutor"),
            "suspended" => rows.Where(r => r.Status == "Suspended"),
            "pending" => rows.Where(r => r.Status == "Pending"),
            _ => rows
        };

        if (!string.IsNullOrWhiteSpace(role))
            filtered = filtered.Where(r => r.Role.Equals(role, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(district))
            filtered = filtered.Where(r => r.District == district);

        if (!string.IsNullOrWhiteSpace(status))
            filtered = filtered.Where(r => r.Status.Equals(status, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            filtered = filtered.Where(r =>
                r.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.Email.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.IdCode.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        filtered = registered switch
        {
            "last7" => filtered.Where(r => r.JoinedAt >= now.AddDays(-7)),
            "last30" => filtered.Where(r => r.JoinedAt >= now.AddDays(-30)),
            "last90" => filtered.Where(r => r.JoinedAt >= now.AddDays(-90)),
            _ => filtered
        };

        var sorted = sort switch
        {
            "name_desc" => filtered.OrderByDescending(r => r.Name),
            "sessions_desc" => filtered.OrderByDescending(r => r.SessionCount),
            "sessions_asc" => filtered.OrderBy(r => r.SessionCount),
            "joined_desc" => filtered.OrderByDescending(r => r.JoinedAt),
            "joined_asc" => filtered.OrderBy(r => r.JoinedAt),
            _ => filtered.OrderBy(r => r.Name)
        };

        var sortedList = sorted.ToList();
        var totalMatching = sortedList.Count;

        pageSize = pageSize is 8 or 20 or 50 or 100 ? pageSize : 8;
        var totalPages = totalMatching == 0 ? 1 : (int)Math.Ceiling(totalMatching / (double)pageSize);
        page = Math.Clamp(page, 1, totalPages);

        var pageRows = sortedList.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        for (var i = 0; i < pageRows.Count; i++)
        {
            pageRows[i].AvatarClass = (i % 3) switch { 1 => "purple", 2 => "yellow", _ => "" };
        }

        var pageWindow = new List<int?>();
        if (totalPages <= 1)
        {
            pageWindow.Add(1);
        }
        else
        {
            pageWindow.Add(1);
            if (page <= 3)
            {
                for (var i = 2; i <= Math.Min(3, totalPages - 1); i++) pageWindow.Add(i);
            }
            else
            {
                pageWindow.Add(null);
                var start = Math.Max(2, page - 1);
                var end = Math.Min(totalPages - 1, page + 1);
                for (var i = start; i <= end; i++) pageWindow.Add(i);
            }
            if (page < totalPages - 2) pageWindow.Add(null);
            pageWindow.Add(totalPages);
        }

        var vm = new AdminUserManagementViewModel
        {
            AdminName = admin?.FullName ?? "Administrator",
            AdminInitials = GetInitials(admin?.FullName ?? "Administrator"),
            TotalUsers = totalUsers,
            TotalStudents = totalStudents,
            TotalTutors = totalTutors,
            PendingApprovalCount = pendingApproval,
            SuspendedCount = suspendedCount,
            TotalUsersTrendPercent = totalUsersTrend,
            StudentsTrendPercent = studentsTrend,
            TutorsTrendPercent = tutorsTrend,
            ActiveTab = tab,
            Search = search,
            RoleFilter = role,
            DistrictFilter = district,
            StatusFilter = status,
            RegisteredFilter = registered,
            Sort = sort,
            Districts = districts,
            Rows = pageRows,
            Page = page,
            PageSize = pageSize,
            TotalMatching = totalMatching,
            PageWindow = pageWindow
        };

        return View(vm);
    }

    // ── 5b: Tutor Verification ────────────────────────────────────────────

    public async Task<IActionResult> TutorVerification(
        string tab = "pending",
        string? search = null,
        string? subject = null,
        string? district = null,
        string submitted = "all",
        string sort = "newest",
        int visibleCount = 3)
    {
        await SetAdminNotificationBellAsync();
        var admin = await _userManager.GetUserAsync(User);
        var now = DateTime.Now;
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var previousMonthStart = monthStart.AddMonths(-1);

        var allTutors = await _context.TutorProfiles
            .Include(t => t.User)
            .ToListAsync();

        var pendingTabCount = allTutors.Count(t => !t.IsVerified && !t.VerificationRejected);
        var approvedTabCount = allTutors.Count(t => t.IsVerified);
        var rejectedTabCount = allTutors.Count(t => t.VerificationRejected);
        var allTabCount = allTutors.Count;

        var approvedThisMonth = allTutors.Count(t => t.IsVerified && t.VerificationDecidedAt >= monthStart);
        var approvedPreviousMonth = allTutors.Count(t =>
            t.IsVerified && t.VerificationDecidedAt >= previousMonthStart && t.VerificationDecidedAt < monthStart);
        int? approvedTrend = approvedPreviousMonth == 0
            ? null
            : (int)Math.Round(100.0 * (approvedThisMonth - approvedPreviousMonth) / approvedPreviousMonth);

        var rejectedThisMonth = allTutors.Count(t => t.VerificationRejected && t.VerificationDecidedAt >= monthStart);
        var decidedThisMonth = approvedThisMonth + rejectedThisMonth;
        int? rejectedRate = decidedThisMonth == 0
            ? null
            : (int)Math.Round(100.0 * rejectedThisMonth / decidedThisMonth);

        var reviewDurations = allTutors
            .Where(t => (t.IsVerified || t.VerificationRejected) && t.VerificationDecidedAt.HasValue)
            .Select(t => (t.VerificationDecidedAt!.Value - t.User.CreatedAt).TotalDays)
            .Where(d => d >= 0)
            .ToList();
        double? avgReviewTimeDays = reviewDurations.Count == 0 ? null : Math.Round(reviewDurations.Average(), 1);

        var totalDecisions = approvedTabCount + rejectedTabCount;
        var approvalRate = totalDecisions == 0 ? 0 : (int)Math.Round(100.0 * approvedTabCount / totalDecisions);

        IEnumerable<TutorProfile> filtered = tab switch
        {
            "approved" => allTutors.Where(t => t.IsVerified),
            "rejected" => allTutors.Where(t => t.VerificationRejected),
            "all" => allTutors,
            _ => allTutors.Where(t => !t.IsVerified && !t.VerificationRejected)
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            filtered = filtered.Where(t =>
                t.User.FullName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (t.User.Email ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                t.Subjects.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(subject))
        {
            filtered = filtered.Where(t => t.Subjects
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(s => s.Equals(subject, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(district))
            filtered = filtered.Where(t => t.User.District == district);

        filtered = submitted switch
        {
            "last7" => filtered.Where(t => t.User.CreatedAt >= now.AddDays(-7)),
            "last30" => filtered.Where(t => t.User.CreatedAt >= now.AddDays(-30)),
            "last90" => filtered.Where(t => t.User.CreatedAt >= now.AddDays(-90)),
            _ => filtered
        };

        var sorted = sort switch
        {
            "oldest" => filtered.OrderBy(t => t.User.CreatedAt),
            "name_asc" => filtered.OrderBy(t => t.User.FullName),
            "name_desc" => filtered.OrderByDescending(t => t.User.FullName),
            _ => filtered.OrderByDescending(t => t.User.CreatedAt)
        };

        var sortedList = sorted.ToList();
        var totalMatching = sortedList.Count;

        visibleCount = Math.Max(3, visibleCount);
        var pageTutors = sortedList.Take(visibleCount).ToList();

        var visibleTutorIds = pageTutors.Select(t => t.Id).ToList();
        var credentialsByTutor = await _context.TutorCredentials
            .Where(c => visibleTutorIds.Contains(c.TutorProfileId) && c.DocumentType != null)
            .ToListAsync();
        var credentialLookup = credentialsByTutor
            .GroupBy(c => c.TutorProfileId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var rows = pageTutors.Select(t =>
        {
            var daysAgo = (int)(now.Date - t.User.CreatedAt.Date).TotalDays;
            var tutorCredentials = credentialLookup.TryGetValue(t.Id, out var creds) ? creds : new List<TutorCredential>();

            var documents = RequiredVerificationDocuments.Select(rd =>
            {
                var match = tutorCredentials.FirstOrDefault(c => c.DocumentType == rd.Type);
                return new AdminTutorVerificationDocumentViewModel
                {
                    CredentialId = match?.Id,
                    Label = rd.Label,
                    Icon = rd.Icon,
                    IsMissing = match == null
                };
            }).ToList();

            return new AdminTutorVerificationRowViewModel
            {
                TutorProfileId = t.Id,
                UserId = t.UserId,
                Name = t.User.FullName,
                Initials = GetInitials(t.User.FullName),
                Email = t.User.Email ?? "",
                MaskedPhone = MaskPhone(t.User.PhoneNumber),
                District = t.User.District,
                Subjects = t.Subjects.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
                Education = t.Education,
                ExperienceSummary = t.ExperienceSummary,
                YearsOfExperience = t.YearsOfExperience,
                SubmittedAt = t.User.CreatedAt,
                DaysAgo = daysAgo,
                UrgencyClass = daysAgo >= 2 ? "red" : "orange",
                Documents = documents,
                Status = t.IsVerified ? "Approved" : t.VerificationRejected ? "Rejected" : "Pending",
                VerificationNote = t.VerificationNote,
                AllDocumentsUploaded = documents.All(d => !d.IsMissing),
                InterviewScheduledAt = t.InterviewScheduledAt,
                InterviewMeetingLink = t.InterviewMeetingLink,
                InterviewCompletedAt = t.InterviewCompletedAt
            };
        }).ToList();

        var subjectOptions = allTutors
            .SelectMany(t => t.Subjects.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        var districtOptions = allTutors
            .Select(t => t.User.District)
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Select(d => d!)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        var vm = new AdminTutorVerificationViewModel
        {
            AdminName = admin?.FullName ?? "Administrator",
            AdminInitials = GetInitials(admin?.FullName ?? "Administrator"),
            MonthLabel = now.ToString("MMMM"),
            PendingCount = pendingTabCount,
            ApprovedThisMonth = approvedThisMonth,
            ApprovedTrendPercent = approvedTrend,
            RejectedThisMonth = rejectedThisMonth,
            RejectedRatePercent = rejectedRate,
            AvgReviewTimeDays = avgReviewTimeDays,
            ApprovalRatePercent = approvalRate,
            ActiveTab = tab,
            PendingTabCount = pendingTabCount,
            ApprovedTabCount = approvedTabCount,
            RejectedTabCount = rejectedTabCount,
            AllTabCount = allTabCount,
            Search = search,
            SubjectFilter = subject,
            DistrictFilter = district,
            SubmittedFilter = submitted,
            Sort = sort,
            Subjects = subjectOptions,
            Districts = districtOptions,
            Applications = rows,
            VisibleCount = visibleCount,
            TotalMatching = totalMatching
        };

        return View(vm);
    }

    public async Task<IActionResult> ExportTutorVerificationCsv(
        string tab = "pending",
        string? search = null,
        string? subject = null,
        string? district = null,
        string submitted = "all")
    {
        var now = DateTime.Now;
        var allTutors = await _context.TutorProfiles.Include(t => t.User).ToListAsync();

        IEnumerable<TutorProfile> filtered = tab switch
        {
            "approved" => allTutors.Where(t => t.IsVerified),
            "rejected" => allTutors.Where(t => t.VerificationRejected),
            "all" => allTutors,
            _ => allTutors.Where(t => !t.IsVerified && !t.VerificationRejected)
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            filtered = filtered.Where(t =>
                t.User.FullName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (t.User.Email ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                t.Subjects.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(subject))
        {
            filtered = filtered.Where(t => t.Subjects
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(s => s.Equals(subject, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(district))
            filtered = filtered.Where(t => t.User.District == district);

        filtered = submitted switch
        {
            "last7" => filtered.Where(t => t.User.CreatedAt >= now.AddDays(-7)),
            "last30" => filtered.Where(t => t.User.CreatedAt >= now.AddDays(-30)),
            "last90" => filtered.Where(t => t.User.CreatedAt >= now.AddDays(-90)),
            _ => filtered
        };

        var csv = new System.Text.StringBuilder();
        csv.AppendLine("Name,Email,District,Subjects,YearsOfExperience,Submitted,Status");
        foreach (var t in filtered.OrderByDescending(t => t.User.CreatedAt))
        {
            string Esc(string? v) => "\"" + (v ?? "").Replace("\"", "\"\"") + "\"";
            var status = t.IsVerified ? "Approved" : t.VerificationRejected ? "Rejected" : "Pending";
            csv.AppendLine(string.Join(",",
                Esc(t.User.FullName), Esc(t.User.Email), Esc(t.User.District), Esc(t.Subjects),
                Esc(t.YearsOfExperience.ToString()), Esc(t.User.CreatedAt.ToString("yyyy-MM-dd")), Esc(status)));
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
        return File(bytes, "text/csv", $"tutorbridge-verification-{DateTime.Now:yyyyMMdd-HHmm}.csv");
    }

    // ── Document viewer ───────────────────────────────────────────────────

    // Served inline so the browser renders the PDF/image directly in a new
    // tab rather than downloading it.
    public async Task<IActionResult> DownloadVerificationDocument(int credentialId)
    {
        var credential = await _context.TutorCredentials.FirstOrDefaultAsync(c => c.Id == credentialId);
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

    // ── User actions ──────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddUser(RegisterViewModel model, string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(model.FullName) || string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Password))
        {
            TempData["SettingsError"] = "Full name, email and password are required.";
            return Redirect(returnUrl ?? Url.Action("UserManagement")!);
        }

        if (model.Password != model.ConfirmPassword)
        {
            TempData["SettingsError"] = "Password and confirmation do not match.";
            return Redirect(returnUrl ?? Url.Action("UserManagement")!);
        }

        if (model.Role is not ("Student" or "Tutor"))
        {
            TempData["SettingsError"] = "Choose a role for the new user.";
            return Redirect(returnUrl ?? Url.Action("UserManagement")!);
        }

        var existing = await _userManager.FindByEmailAsync(model.Email);
        if (existing != null)
        {
            TempData["SettingsError"] = "A user with that email already exists.";
            return Redirect(returnUrl ?? Url.Action("UserManagement")!);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email.Trim(),
            Email = model.Email.Trim(),
            FullName = model.FullName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim(),
            District = string.IsNullOrWhiteSpace(model.District) ? null : model.District.Trim(),
            // Admin-created directly - there's no confirmation-email pipeline to
            // send one through, so there's nothing to "confirm" here.
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            TempData["SettingsError"] = string.Join(" ", result.Errors.Select(e => e.Description));
            return Redirect(returnUrl ?? Url.Action("UserManagement")!);
        }

        await _userManager.AddToRoleAsync(user, model.Role);

        if (model.Role == "Tutor")
        {
            _context.TutorProfiles.Add(new TutorProfile
            {
                UserId = user.Id,
                Subjects = model.Subjects ?? "",
                YearsOfExperience = model.YearsOfExperience,
                // The admin is vouching for this account directly, so it skips
                // the normal verification queue rather than sitting there
                // "pending" with no submitted documents behind it.
                IsVerified = true
            });
        }
        else
        {
            _context.StudentProfiles.Add(new StudentProfile
            {
                UserId = user.Id,
                GradeLevel = model.GradeLevel
            });
        }

        await _context.SaveChangesAsync();

        NotificationHelper.Create(_context,
            type: "System",
            title: model.Role == "Tutor" ? "Tutor added by admin" : "Student added by admin",
            message: $"{user.FullName} was added directly by an administrator.",
            icon: model.Role == "Tutor" ? "🧑‍🏫" : "🧑‍🎓",
            actionLabel: "View profile",
            actionUrl: Url.Action("UserManagement", new { search = user.Email }));

        await _context.SaveChangesAsync();

        TempData["SettingsSuccess"] = $"{user.FullName} was added as a {model.Role.ToLower()}.";
        return Redirect(returnUrl ?? Url.Action("UserManagement")!);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SuspendUser(string userId, string? returnUrl)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user != null)
        {
            user.IsSuspended = true;
            await _context.SaveChangesAsync();
        }

        return LocalRedirect(string.IsNullOrWhiteSpace(returnUrl) ? Url.Action("UserManagement")! : returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReactivateUser(string userId, string? returnUrl)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user != null)
        {
            user.IsSuspended = false;
            await _context.SaveChangesAsync();
        }

        return LocalRedirect(string.IsNullOrWhiteSpace(returnUrl) ? Url.Action("UserManagement")! : returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkUserAction(List<string> selectedUserIds, string bulkAction, string? returnUrl)
    {
        if (selectedUserIds != null && selectedUserIds.Count > 0)
        {
            if (bulkAction == "activate" || bulkAction == "suspend")
            {
                var users = await _context.Users.Where(u => selectedUserIds.Contains(u.Id)).ToListAsync();
                foreach (var user in users)
                {
                    user.IsSuspended = bulkAction == "suspend";
                }
                await _context.SaveChangesAsync();
            }
            else if (bulkAction == "delete")
            {
                // Never let an admin delete their own account through this
                // path - Settings > Danger zone is the deliberate place for that.
                var currentAdminId = _userManager.GetUserId(User);
                var idsToDelete = selectedUserIds.Where(id => id != currentAdminId).ToList();

                var users = await _context.Users.Where(u => idsToDelete.Contains(u.Id)).ToListAsync();
                foreach (var user in users)
                {
                    var roles = await _userManager.GetRolesAsync(user);
                    var role = roles.Contains("Tutor") ? "Tutor" : roles.Contains("Student") ? "Student" : null;
                    if (role != null)
                    {
                        await DeleteUserAccountAsync(user, role);
                    }
                }

                TempData["SettingsSuccess"] = $"{users.Count} user{(users.Count == 1 ? "" : "s")} deleted.";
            }
        }

        return LocalRedirect(string.IsNullOrWhiteSpace(returnUrl) ? Url.Action("UserManagement")! : returnUrl);
    }

    public async Task<IActionResult> ExportUsersCsv(
        string tab = "all",
        string? search = null,
        string? role = null,
        string? district = null,
        string? status = null,
        string registered = "all")
    {
        var now = DateTime.Now;
        var studentProfiles = await _context.StudentProfiles.Include(s => s.User).ToListAsync();
        var tutorProfiles = await _context.TutorProfiles.Include(t => t.User).ToListAsync();

        var rows = new List<AdminUserRowFullViewModel>();
        rows.AddRange(studentProfiles.Select(s => new AdminUserRowFullViewModel
        {
            Name = s.User.FullName,
            Email = s.User.Email ?? "",
            Role = "Student",
            IdCode = $"STU-{s.Id:D4}",
            District = s.User.District,
            JoinedAt = s.User.CreatedAt,
            Status = s.User.IsSuspended ? "Suspended" : "Active"
        }));
        rows.AddRange(tutorProfiles.Select(t => new AdminUserRowFullViewModel
        {
            Name = t.User.FullName,
            Email = t.User.Email ?? "",
            Role = "Tutor",
            IdCode = $"TUT-{t.Id:D4}",
            District = t.User.District,
            JoinedAt = t.User.CreatedAt,
            Status = t.User.IsSuspended ? "Suspended" : (t.VerificationRejected ? "Rejected" : (!t.IsVerified ? "Pending" : "Active"))
        }));

        IEnumerable<AdminUserRowFullViewModel> filtered = tab switch
        {
            "students" => rows.Where(r => r.Role == "Student"),
            "tutors" => rows.Where(r => r.Role == "Tutor"),
            "suspended" => rows.Where(r => r.Status == "Suspended"),
            "pending" => rows.Where(r => r.Status == "Pending"),
            _ => rows
        };

        if (!string.IsNullOrWhiteSpace(role))
            filtered = filtered.Where(r => r.Role.Equals(role, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(district))
            filtered = filtered.Where(r => r.District == district);
        if (!string.IsNullOrWhiteSpace(status))
            filtered = filtered.Where(r => r.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            filtered = filtered.Where(r =>
                r.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.Email.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.IdCode.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        filtered = registered switch
        {
            "last7" => filtered.Where(r => r.JoinedAt >= now.AddDays(-7)),
            "last30" => filtered.Where(r => r.JoinedAt >= now.AddDays(-30)),
            "last90" => filtered.Where(r => r.JoinedAt >= now.AddDays(-90)),
            _ => filtered
        };

        var csv = new System.Text.StringBuilder();
        csv.AppendLine("Name,Email,Role,ID,District,Joined,Status");
        foreach (var r in filtered.OrderBy(r => r.Name))
        {
            string Esc(string? v) => "\"" + (v ?? "").Replace("\"", "\"\"") + "\"";
            csv.AppendLine(string.Join(",", Esc(r.Name), Esc(r.Email), Esc(r.Role), Esc(r.IdCode), Esc(r.District), Esc(r.JoinedAt.ToString("yyyy-MM-dd")), Esc(r.Status)));
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
        return File(bytes, "text/csv", $"tutorbridge-users-{DateTime.Now:yyyyMMdd-HHmm}.csv");
    }

    // ── 5c: Tutor approve / reject (now stamps VerificationDecidedAt) ─────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveTutor(int tutorProfileId, string? returnUrl)
    {
        var tutor = await _context.TutorProfiles.Include(t => t.User).FirstOrDefaultAsync(t => t.Id == tutorProfileId);

        if (tutor != null && !tutor.InterviewCompletedAt.HasValue)
        {
            TempData["SettingsError"] = "Confirm the interview is complete before approving this application.";
            return string.IsNullOrWhiteSpace(returnUrl) ? RedirectToAction("Dashboard") : LocalRedirect(returnUrl);
        }

        if (tutor != null)
        {
            var platformSettings = await GetOrCreatePlatformSettingsAsync();
            if (platformSettings.RequirePoliceReportForTutors)
            {
                var hasPoliceReport = await _context.TutorCredentials
                    .AnyAsync(c => c.TutorProfileId == tutorProfileId && c.DocumentType == "PoliceReport");
                if (!hasPoliceReport)
                {
                    TempData["SettingsError"] = $"Can't approve {tutor.User.FullName} - a police report is required before approval (Settings > Platform configuration).";
                    return string.IsNullOrWhiteSpace(returnUrl) ? RedirectToAction("TutorVerification") : LocalRedirect(returnUrl);
                }
            }

            tutor.IsVerified = true;
            tutor.VerificationRejected = false;
            tutor.VerificationDecidedAt = DateTime.Now;

            NotificationHelper.Create(_context,
                type: "Verification",
                title: "Tutor application approved",
                message: $"{tutor.User.FullName}'s application for {tutor.Subjects} was approved",
                icon: "✔️");

            await _context.SaveChangesAsync();

            await SendTutorVerificationEmailAsync(tutor.User, "Welcome to TutorBridge Nepal!", $@"
                <p>Hi {System.Net.WebUtility.HtmlEncode(tutor.User.FullName)},</p>
                <p>🎉 Congratulations — you've been approved as a tutor on TutorBridge Nepal. Welcome to the board!</p>
                <p>Your profile is now live and students can start booking sessions with you.</p>
                <p>— TutorBridge Nepal</p>");

            TempData["SettingsSuccess"] = "Application approved and the tutor has been notified by email.";
        }

        return string.IsNullOrWhiteSpace(returnUrl) ? RedirectToAction("Dashboard") : LocalRedirect(returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectTutor(int tutorProfileId, string reason, string? returnUrl)
    {
        var tutor = await _context.TutorProfiles.Include(t => t.User).FirstOrDefaultAsync(t => t.Id == tutorProfileId);

        if (tutor != null && !tutor.InterviewCompletedAt.HasValue)
        {
            TempData["SettingsError"] = "Confirm the interview is complete before rejecting this application.";
            return string.IsNullOrWhiteSpace(returnUrl) ? RedirectToAction("Dashboard") : LocalRedirect(returnUrl);
        }

        if (tutor != null)
        {
            var tutorUser = tutor.User;
            var tutorName = tutorUser.FullName;
            var trimmedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

            NotificationHelper.Create(_context,
                type: "Verification",
                title: "Tutor application rejected",
                message: $"{tutorName}'s application was rejected" + (trimmedReason == null ? "" : $": {trimmedReason}") + " - the account has been removed.",
                icon: "✖️");

            await SendTutorVerificationEmailAsync(tutorUser, "Your TutorBridge Nepal application", $@"
                <p>Hi {System.Net.WebUtility.HtmlEncode(tutorName)},</p>
                <p>Thank you for applying and interviewing with TutorBridge Nepal. After careful review, we're not able to move forward with your application at this time.</p>
                {(trimmedReason != null ? $"<p><strong>Reason:</strong> {System.Net.WebUtility.HtmlEncode(trimmedReason)}</p>" : "")}
                <p>You're welcome to register again in the future with updated information.</p>
                <p>— TutorBridge Nepal</p>");

            // Hard-deletes the tutor profile, credentials, uploaded documents
            // and the account itself (this also saves the notification queued
            // above), so the same email address is free to register again.
            await DeleteUserAccountAsync(tutorUser, "Tutor");

            TempData["SettingsSuccess"] = "Application rejected. The tutor has been notified by email and the account removed.";
        }

        return string.IsNullOrWhiteSpace(returnUrl) ? RedirectToAction("Dashboard") : LocalRedirect(returnUrl);
    }

    // Doesn't approve or reject - keeps the application in "Pending" but
    // attaches a note the tutor sees on their VerificationPending page,
    // explaining what needs fixing. The note clears automatically the next
    // time the tutor uploads a document (see
    // TutorController.UploadVerificationDocument), since a fresh upload is
    // how the tutor "responds".
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestMoreInfoTutor(int tutorProfileId, string note, string? returnUrl)
    {
        var tutor = await _context.TutorProfiles.Include(t => t.User).FirstOrDefaultAsync(t => t.Id == tutorProfileId);
        if (tutor != null && !string.IsNullOrWhiteSpace(note))
        {
            var trimmedNote = note.Trim();
            tutor.VerificationNote = trimmedNote;
            await _context.SaveChangesAsync();

            await SendTutorVerificationEmailAsync(tutor.User, "TutorBridge Nepal needs more information", $@"
                <p>Hi {System.Net.WebUtility.HtmlEncode(tutor.User.FullName)},</p>
                <p>We're reviewing your tutor application and need a bit more information before we can continue:</p>
                <p><strong>{System.Net.WebUtility.HtmlEncode(trimmedNote)}</strong></p>
                <p>Please log in and update your application with the requested information.</p>
                <p>— TutorBridge Nepal</p>");

            TempData["SettingsSuccess"] = $"Request sent to {tutor.User.FullName} - they've been notified by email.";
        }

        return string.IsNullOrWhiteSpace(returnUrl) ? RedirectToAction("TutorVerification") : LocalRedirect(returnUrl);
    }

    public async Task<IActionResult> UserDetail(string userId, string? returnUrl)
    {
        await SetAdminNotificationBellAsync();
        var admin = await _userManager.GetUserAsync(User);

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return NotFound();

        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.Contains("Tutor") ? "Tutor" : roles.Contains("Student") ? "Student" : (roles.FirstOrDefault() ?? "Unknown");

        var vm = new AdminUserDetailViewModel
        {
            UserId = user.Id,
            FullName = user.FullName,
            Initials = GetInitials(user.FullName),
            Email = user.Email ?? "",
            PhoneNumber = user.PhoneNumber,
            District = user.District,
            Role = role,
            JoinedAt = user.CreatedAt,
            IsSuspended = user.IsSuspended
        };

        if (role == "Tutor")
        {
            var tutor = await _context.TutorProfiles.FirstOrDefaultAsync(t => t.UserId == userId);
            if (tutor == null) return NotFound();

            var credentials = await _context.TutorCredentials
                .Where(c => c.TutorProfileId == tutor.Id)
                .OrderBy(c => c.SortOrder)
                .Select(c => c.Title)
                .ToListAsync();

            var bookings = await _context.Bookings
                .Include(b => b.TutorAvailabilitySlot)
                .Include(b => b.StudentProfile).ThenInclude(s => s.User)
                .Where(b => b.TutorProfileId == tutor.Id)
                .OrderByDescending(b => b.TutorAvailabilitySlot.StartTime)
                .ToListAsync();

            var nonCancelled = bookings.Where(b => b.Status != "Cancelled").ToList();
            var completed = nonCancelled.Where(b => b.Status == "Completed").ToList();

            vm.TutorProfileId = tutor.Id;
            vm.IdCode = $"TUT-{tutor.Id:D4}";
            vm.Subjects = tutor.Subjects;
            vm.YearsOfExperience = tutor.YearsOfExperience;
            vm.AverageRating = tutor.AverageRating;
            vm.ReviewCount = tutor.ReviewCount;
            vm.Bio = tutor.Bio;
            vm.IsVerified = tutor.IsVerified;
            vm.VerificationRejected = tutor.VerificationRejected;
            vm.VerificationDecidedAt = tutor.VerificationDecidedAt;
            vm.CredentialTitles = credentials;
            vm.Status = user.IsSuspended ? "Suspended" : (tutor.VerificationRejected ? "Rejected" : (!tutor.IsVerified ? "Pending" : "Active"));
            vm.TotalSessions = nonCancelled.Count;
            vm.CompletedSessions = completed.Count;
            vm.HoursLearned = Math.Round(completed.Sum(b => (b.TutorAvailabilitySlot.EndTime - b.TutorAvailabilitySlot.StartTime).TotalHours), 1);
            vm.RecentSessions = nonCancelled.Take(15).Select(b => new AdminUserSessionRow
            {
                OtherPartyName = b.StudentProfile.User.FullName,
                Subject = b.Subject,
                Date = b.TutorAvailabilitySlot.StartTime,
                Status = b.Status
            }).ToList();
        }
        else
        {
            var student = await _context.StudentProfiles.FirstOrDefaultAsync(s => s.UserId == userId);
            if (student == null) return NotFound();

            var bookings = await _context.Bookings
                .Include(b => b.TutorAvailabilitySlot)
                .Include(b => b.TutorProfile).ThenInclude(t => t.User)
                .Where(b => b.StudentProfileId == student.Id)
                .OrderByDescending(b => b.TutorAvailabilitySlot.StartTime)
                .ToListAsync();

            var nonCancelled = bookings.Where(b => b.Status != "Cancelled").ToList();
            var completed = nonCancelled.Where(b => b.Status == "Completed").ToList();

            vm.StudentProfileId = student.Id;
            vm.IdCode = $"STU-{student.Id:D4}";
            vm.GradeLevel = student.GradeLevel;
            vm.SchoolName = student.SchoolName;
            vm.CurriculumBoard = student.CurriculumBoard;
            vm.Status = user.IsSuspended ? "Suspended" : "Active";
            vm.TotalSessions = nonCancelled.Count;
            vm.CompletedSessions = completed.Count;
            vm.HoursLearned = Math.Round(completed.Sum(b => (b.TutorAvailabilitySlot.EndTime - b.TutorAvailabilitySlot.StartTime).TotalHours), 1);
            vm.RecentSessions = nonCancelled.Take(15).Select(b => new AdminUserSessionRow
            {
                OtherPartyName = b.TutorProfile.User.FullName,
                Subject = b.Subject,
                Date = b.TutorAvailabilitySlot.StartTime,
                Status = b.Status
            }).ToList();
        }

        ViewData["AdminName"] = admin?.FullName ?? "Administrator";
        ViewData["AdminInitials"] = GetInitials(admin?.FullName ?? "Administrator");
        ViewData["ReturnUrl"] = returnUrl;
        return View(vm);
    }

    // ── Session Logs ──────────────────────────────────────────────────────

    // Turns a raw Booking.Status + timing into what the Session Logs page
    // actually shows - a disputed session always wins regardless of its
    // underlying status, and "Confirmed" splits into Live/Upcoming/Completed
    // depending on where "now" falls relative to the slot.
    private static string ComputeSessionDisplayStatus(Booking b, DateTime now)
    {
        if (b.IsDisputed) return "Disputed";
        if (b.Status == "Cancelled") return "Cancelled";
        if (b.Status == "Completed") return "Completed";
        if (b.Status == "Missed") return "Missed";

        if (b.Status == "Confirmed")
        {
            if (b.TutorAvailabilitySlot.StartTime <= now && b.TutorAvailabilitySlot.EndTime >= now) return "Live";
            if (b.TutorAvailabilitySlot.StartTime > now) return "Upcoming";
            return "Completed"; // slot time has passed but never explicitly marked
        }

        return "Pending";
    }

    public async Task<IActionResult> SessionLogs(
        string tab = "all",
        string? search = null,
        string? subject = null,
        string? status = null,
        string dateRange = "all",
        string? district = null,
        string sort = "newest",
        int page = 1,
        int pageSize = 8)
    {
        await SetAdminNotificationBellAsync();
        var admin = await _userManager.GetUserAsync(User);
        var now = DateTime.Now;
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var previousMonthStart = monthStart.AddMonths(-1);

        var allBookings = await _context.Bookings
            .Include(b => b.StudentProfile).ThenInclude(s => s.User)
            .Include(b => b.TutorProfile).ThenInclude(t => t.User)
            .Include(b => b.TutorAvailabilitySlot)
            .ToListAsync();

        // ---- KPI strip ----
        var totalSessions = allBookings.Count;
        var createdThisMonth = allBookings.Count(b => b.CreatedAt >= monthStart);
        var createdPreviousMonth = allBookings.Count(b => b.CreatedAt >= previousMonthStart && b.CreatedAt < monthStart);
        int? totalTrend = createdPreviousMonth == 0
            ? null
            : (int)Math.Round(100.0 * (createdThisMonth - createdPreviousMonth) / createdPreviousMonth);

        var completedCount = allBookings.Count(b => ComputeSessionDisplayStatus(b, now) == "Completed");
        var ongoingNowCount = allBookings.Count(b => ComputeSessionDisplayStatus(b, now) == "Live");
        var cancelledCount = allBookings.Count(b => b.Status == "Cancelled");
        var disputedCount = allBookings.Count(b => b.IsDisputed);

        var completedPercent = totalSessions == 0 ? 0 : (int)Math.Round(100.0 * completedCount / totalSessions);
        var cancelledPercent = totalSessions == 0 ? 0 : (int)Math.Round(100.0 * cancelledCount / totalSessions);

        // ---- Tab filter ----
        IEnumerable<Booking> filtered = tab switch
        {
            "completed" => allBookings.Where(b => ComputeSessionDisplayStatus(b, now) == "Completed"),
            "ongoing" => allBookings.Where(b => ComputeSessionDisplayStatus(b, now) == "Live"),
            "cancelled" => allBookings.Where(b => b.Status == "Cancelled"),
            "disputed" => allBookings.Where(b => b.IsDisputed),
            _ => allBookings
        };

        // ---- Explicit filters ----
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            filtered = filtered.Where(b =>
                b.StudentProfile.User.FullName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                b.TutorProfile.User.FullName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                $"SES-{b.Id:D4}".Contains(term, StringComparison.OrdinalIgnoreCase) ||
                b.Id.ToString() == term);
        }

        if (!string.IsNullOrWhiteSpace(subject))
            filtered = filtered.Where(b => b.Subject == subject);

        if (!string.IsNullOrWhiteSpace(status))
            filtered = filtered.Where(b => ComputeSessionDisplayStatus(b, now) == status);

        if (!string.IsNullOrWhiteSpace(district))
            filtered = filtered.Where(b => b.StudentProfile.User.District == district || b.TutorProfile.User.District == district);

        filtered = dateRange switch
        {
            "last7" => filtered.Where(b => b.TutorAvailabilitySlot.StartTime >= now.AddDays(-7)),
            "last30" => filtered.Where(b => b.TutorAvailabilitySlot.StartTime >= now.AddDays(-30)),
            "last90" => filtered.Where(b => b.TutorAvailabilitySlot.StartTime >= now.AddDays(-90)),
            _ => filtered
        };

        var sorted = sort switch
        {
            "oldest" => filtered.OrderBy(b => b.TutorAvailabilitySlot.StartTime),
            "student_asc" => filtered.OrderBy(b => b.StudentProfile.User.FullName),
            "tutor_asc" => filtered.OrderBy(b => b.TutorProfile.User.FullName),
            _ => filtered.OrderByDescending(b => b.TutorAvailabilitySlot.StartTime) // "newest"
        };

        var sortedList = sorted.ToList();
        var totalMatching = sortedList.Count;

        pageSize = pageSize is 8 or 20 or 50 or 100 ? pageSize : 8;
        var totalPages = totalMatching == 0 ? 1 : (int)Math.Ceiling(totalMatching / (double)pageSize);
        page = Math.Clamp(page, 1, totalPages);

        var pageRows = sortedList.Skip((page - 1) * pageSize).Take(pageSize).Select(b => new AdminSessionLogRowViewModel
        {
            BookingId = b.Id,
            StudentName = b.StudentProfile.User.FullName,
            StudentInitials = GetInitials(b.StudentProfile.User.FullName),
            TutorName = b.TutorProfile.User.FullName,
            TutorInitials = GetInitials(b.TutorProfile.User.FullName),
            Subject = b.Subject,
            StartTime = b.TutorAvailabilitySlot.StartTime,
            EndTime = b.TutorAvailabilitySlot.EndTime,
            DurationLabel = $"{(int)(b.TutorAvailabilitySlot.EndTime - b.TutorAvailabilitySlot.StartTime).TotalHours}h {(b.TutorAvailabilitySlot.EndTime - b.TutorAvailabilitySlot.StartTime).Minutes:D2}m",
            DisplayStatus = ComputeSessionDisplayStatus(b, now),
            Mode = b.TutorAvailabilitySlot.Mode,
            IsDisputed = b.IsDisputed
        }).ToList();

        var pageWindow = new List<int?>();
        if (totalPages <= 1)
        {
            pageWindow.Add(1);
        }
        else
        {
            pageWindow.Add(1);
            if (page <= 3)
            {
                for (var i = 2; i <= Math.Min(3, totalPages - 1); i++) pageWindow.Add(i);
            }
            else
            {
                pageWindow.Add(null);
                var start = Math.Max(2, page - 1);
                var end = Math.Min(totalPages - 1, page + 1);
                for (var i = start; i <= end; i++) pageWindow.Add(i);
            }
            if (page < totalPages - 2) pageWindow.Add(null);
            pageWindow.Add(totalPages);
        }

        // ---- Chart: sessions over the last 14 days (non-cancelled) ----
        var chartStart = now.Date.AddDays(-13);
        var chartCounts = allBookings
            .Where(b => b.Status != "Cancelled" && b.TutorAvailabilitySlot.StartTime.Date >= chartStart && b.TutorAvailabilitySlot.StartTime.Date <= now.Date)
            .Select(b => b.TutorAvailabilitySlot.StartTime.Date)
            .ToList();

        var chartLabels = new List<string>();
        var chartValues = new List<int>();
        for (var d = chartStart; d <= now.Date; d = d.AddDays(1))
        {
            chartLabels.Add(d.ToString("d MMM"));
            chartValues.Add(chartCounts.Count(x => x == d));
        }

        var subjectOptions = allBookings.Select(b => b.Subject).Distinct().OrderBy(s => s).ToList();
        var districtOptions = allBookings
            .SelectMany(b => new[] { b.StudentProfile.User.District, b.TutorProfile.User.District })
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Select(d => d!)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        var vm = new AdminSessionLogsViewModel
        {
            AdminName = admin?.FullName ?? "Administrator",
            AdminInitials = GetInitials(admin?.FullName ?? "Administrator"),
            TotalSessions = totalSessions,
            TotalSessionsTrendPercent = totalTrend,
            CompletedCount = completedCount,
            CompletedPercent = completedPercent,
            OngoingNowCount = ongoingNowCount,
            CancelledCount = cancelledCount,
            CancelledPercent = cancelledPercent,
            ActiveTab = tab,
            AllTabCount = allBookings.Count,
            CompletedTabCount = completedCount,
            OngoingTabCount = ongoingNowCount,
            CancelledTabCount = cancelledCount,
            DisputedTabCount = disputedCount,
            Search = search,
            SubjectFilter = subject,
            StatusFilter = status,
            DateRangeFilter = dateRange,
            DistrictFilter = district,
            Sort = sort,
            Subjects = subjectOptions,
            Districts = districtOptions,
            Rows = pageRows,
            Page = page,
            PageSize = pageSize,
            TotalMatching = totalMatching,
            PageWindow = pageWindow,
            ChartLabels = chartLabels,
            ChartValues = chartValues
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ScheduleTutorInterview(int tutorProfileId, DateTime interviewAt, string meetingLink, string? returnUrl)
    {
        var tutor = await _context.TutorProfiles.Include(t => t.User).FirstOrDefaultAsync(t => t.Id == tutorProfileId);

        if (tutor == null || string.IsNullOrWhiteSpace(meetingLink) || interviewAt <= DateTime.Now)
        {
            TempData["SettingsError"] = "Please provide a valid meeting link and a future date/time.";
            return string.IsNullOrWhiteSpace(returnUrl) ? RedirectToAction("TutorVerification") : LocalRedirect(returnUrl);
        }

        tutor.InterviewScheduledAt = interviewAt;
        tutor.InterviewMeetingLink = meetingLink.Trim();

        NotificationHelper.Create(_context,
            type: "Verification",
            title: "Interview scheduled",
            message: $"Interview scheduled with {tutor.User.FullName} for {interviewAt:d MMM yyyy, h:mm tt}",
            icon: "📅");

        await _context.SaveChangesAsync();

        await SendTutorVerificationEmailAsync(tutor.User, "Your TutorBridge Nepal interview is scheduled", $@"
            <p>Hi {System.Net.WebUtility.HtmlEncode(tutor.User.FullName)},</p>
            <p>Your documents have been reviewed and an interview has been scheduled to complete your tutor application:</p>
            <p><strong>When:</strong> {interviewAt:dddd, d MMMM yyyy 'at' h:mm tt}</p>
            <p><strong>Meeting link:</strong> <a href=""{System.Net.WebUtility.HtmlEncode(meetingLink.Trim())}"">{System.Net.WebUtility.HtmlEncode(meetingLink.Trim())}</a></p>
            <p>Please join a few minutes early. We'll confirm your application status after the interview.</p>
            <p>— TutorBridge Nepal</p>");

        TempData["SettingsSuccess"] = "Interview scheduled and the tutor has been notified.";
        return string.IsNullOrWhiteSpace(returnUrl) ? RedirectToAction("TutorVerification") : LocalRedirect(returnUrl);
    }

    // Confirms the scheduled interview actually took place - required before
    // ApproveTutor/RejectTutor will allow a decision, so an application can
    // never be decided purely off documents without a human interview step.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmTutorInterview(int tutorProfileId, string? returnUrl)
    {
        var tutor = await _context.TutorProfiles.Include(t => t.User).FirstOrDefaultAsync(t => t.Id == tutorProfileId);

        if (tutor == null || !tutor.InterviewScheduledAt.HasValue || tutor.InterviewScheduledAt.Value > DateTime.Now)
        {
            TempData["SettingsError"] = "The scheduled interview time hasn't arrived yet.";
            return string.IsNullOrWhiteSpace(returnUrl) ? RedirectToAction("TutorVerification") : LocalRedirect(returnUrl);
        }

        tutor.InterviewCompletedAt = DateTime.Now;
        await _context.SaveChangesAsync();

        TempData["SettingsSuccess"] = "Interview marked as completed. You can now approve or reject this application.";
        return string.IsNullOrWhiteSpace(returnUrl) ? RedirectToAction("TutorVerification") : LocalRedirect(returnUrl);
    }

    // Admin-initiated flag - lets an admin mark a session as disputed
    // directly (e.g. after a phone/email complaint) without requiring the
    // student or tutor to have filed a ticket through the app first.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FlagSessionDisputed(int bookingId, string? returnUrl)
    {
        var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId);
        if (booking != null)
        {
            booking.IsDisputed = true;
            await _context.SaveChangesAsync();
        }

        return string.IsNullOrWhiteSpace(returnUrl) ? RedirectToAction("SessionLogs") : LocalRedirect(returnUrl);
    }

    // Clears the dispute flag and closes any open tickets tied to this
    // session - the admin's way of saying "handled".
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResolveDispute(int bookingId, string? returnUrl)
    {
        var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId);
        if (booking != null)
        {
            booking.IsDisputed = false;

            var openTickets = await _context.SupportTickets
                            .Where(t => t.BookingId == bookingId && t.Status != "Resolved")
                            .ToListAsync();
            foreach (var ticket in openTickets)
            {
                ticket.Status = "Resolved";
                ticket.ResolvedAt = DateTime.Now;
            }

            await _context.SaveChangesAsync();
        }

        return string.IsNullOrWhiteSpace(returnUrl) ? RedirectToAction("SessionLogs") : LocalRedirect(returnUrl);
    }

    // Read-only lookup for the Session Logs "View" action - returns the
    // details of one session as JSON so the front end can render them in a
    // modal without navigating away from the Session Logs list (no
    // dedicated details view exists, and this keeps "View" from mutating
    // anything, unlike FlagSessionDisputed/ResolveDispute).
    [HttpGet]
    public async Task<IActionResult> SessionDetailsJson(int id)
    {
        var b = await _context.Bookings
            .Include(x => x.StudentProfile).ThenInclude(s => s.User)
            .Include(x => x.TutorProfile).ThenInclude(t => t.User)
            .Include(x => x.TutorAvailabilitySlot)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (b == null) return NotFound();

        var ticket = await _context.SupportTickets
            .Where(t => t.BookingId == id)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync();

        return Json(new
        {
            sessionId = $"SES-{b.Id:D4}",
            studentName = b.StudentProfile.User.FullName,
            studentEmail = b.StudentProfile.User.Email,
            studentDistrict = b.StudentProfile.User.District,
            tutorName = b.TutorProfile.User.FullName,
            tutorEmail = b.TutorProfile.User.Email,
            tutorDistrict = b.TutorProfile.User.District,
            subject = b.Subject,
            date = b.TutorAvailabilitySlot.StartTime.ToString("d MMM yyyy"),
            time = $"{b.TutorAvailabilitySlot.StartTime:h:mm tt} - {b.TutorAvailabilitySlot.EndTime:h:mm tt}",
            mode = b.TutorAvailabilitySlot.Mode,
            status = ComputeSessionDisplayStatus(b, DateTime.Now),
            createdAt = b.CreatedAt.ToString("d MMM yyyy, h:mm tt"),
            decidedAt = b.DecidedAt?.ToString("d MMM yyyy, h:mm tt"),
            declinedByTutor = b.DeclinedByTutor,
            note = b.Note,
            calendarSynced = b.GoogleCalendarEventId != null,
            isDisputed = b.IsDisputed,
            ticketSubject = ticket?.Subject,
            ticketMessage = ticket?.Message,
            ticketStatus = ticket?.Status,
            ticketFiledAt = ticket?.CreatedAt.ToString("d MMM yyyy, h:mm tt")
        });
    }

    public async Task<IActionResult> ExportSessionLogsCsv(
        string tab = "all",
        string? search = null,
        string? subject = null,
        string? status = null,
        string dateRange = "all",
        string? district = null)
    {
        var now = DateTime.Now;

        var allBookings = await _context.Bookings
            .Include(b => b.StudentProfile).ThenInclude(s => s.User)
            .Include(b => b.TutorProfile).ThenInclude(t => t.User)
            .Include(b => b.TutorAvailabilitySlot)
            .ToListAsync();

        IEnumerable<Booking> filtered = tab switch
        {
            "completed" => allBookings.Where(b => ComputeSessionDisplayStatus(b, now) == "Completed"),
            "ongoing" => allBookings.Where(b => ComputeSessionDisplayStatus(b, now) == "Live"),
            "cancelled" => allBookings.Where(b => b.Status == "Cancelled"),
            "disputed" => allBookings.Where(b => b.IsDisputed),
            _ => allBookings
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            filtered = filtered.Where(b =>
                b.StudentProfile.User.FullName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                b.TutorProfile.User.FullName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                $"SES-{b.Id:D4}".Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(subject))
            filtered = filtered.Where(b => b.Subject == subject);

        if (!string.IsNullOrWhiteSpace(status))
            filtered = filtered.Where(b => ComputeSessionDisplayStatus(b, now) == status);

        if (!string.IsNullOrWhiteSpace(district))
            filtered = filtered.Where(b => b.StudentProfile.User.District == district || b.TutorProfile.User.District == district);

        filtered = dateRange switch
        {
            "last7" => filtered.Where(b => b.TutorAvailabilitySlot.StartTime >= now.AddDays(-7)),
            "last30" => filtered.Where(b => b.TutorAvailabilitySlot.StartTime >= now.AddDays(-30)),
            "last90" => filtered.Where(b => b.TutorAvailabilitySlot.StartTime >= now.AddDays(-90)),
            _ => filtered
        };

        var csv = new System.Text.StringBuilder();
        csv.AppendLine("SessionId,Student,Tutor,Subject,StartTime,EndTime,Status,Mode");
        foreach (var b in filtered.OrderByDescending(b => b.TutorAvailabilitySlot.StartTime))
        {
            string Esc(string? v) => "\"" + (v ?? "").Replace("\"", "\"\"") + "\"";
            csv.AppendLine(string.Join(",",
                Esc($"SES-{b.Id:D4}"), Esc(b.StudentProfile.User.FullName), Esc(b.TutorProfile.User.FullName), Esc(b.Subject),
                Esc(b.TutorAvailabilitySlot.StartTime.ToString("yyyy-MM-dd HH:mm")), Esc(b.TutorAvailabilitySlot.EndTime.ToString("yyyy-MM-dd HH:mm")),
                Esc(ComputeSessionDisplayStatus(b, now)), Esc(b.TutorAvailabilitySlot.Mode)));
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
        return File(bytes, "text/csv", $"tutorbridge-session-logs-{DateTime.Now:yyyyMMdd-HHmm}.csv");
    }

    // ── Reports ────────────────────────────────────────────────────────────

    private static DateTime QuarterStart(DateTime d) => new(d.Year, ((d.Month - 1) / 3) * 3 + 1, 1);

    public async Task<IActionResult> Reports(string quarter = "current")
    {
        await SetAdminNotificationBellAsync();
        var admin = await _userManager.GetUserAsync(User);
        var now = DateTime.Now;

        DateTime rangeStart, rangeEnd;
        string quarterLabel;
        switch (quarter)
        {
            case "last":
                var thisQStart = QuarterStart(now);
                rangeStart = thisQStart.AddMonths(-3);
                rangeEnd = thisQStart;
                quarterLabel = "Last quarter";
                break;
            case "thisyear":
                rangeStart = new DateTime(now.Year, 1, 1);
                rangeEnd = now.AddDays(1);
                quarterLabel = "This year";
                break;
            case "all":
                rangeStart = DateTime.MinValue;
                rangeEnd = now.AddDays(1);
                quarterLabel = "All time";
                break;
            default:
                quarter = "current";
                rangeStart = QuarterStart(now);
                rangeEnd = now.AddDays(1);
                quarterLabel = "This quarter";
                break;
        }
        var previousRangeStart = rangeStart == DateTime.MinValue ? DateTime.MinValue : rangeStart.AddDays(-(rangeEnd - rangeStart).TotalDays);
        var previousRangeEnd = rangeStart;

        var allUsers = await _context.Users.ToListAsync();
        var allBookingsFull = await _context.Bookings
            .Include(b => b.StudentProfile).ThenInclude(s => s.User)
            .Include(b => b.TutorProfile).ThenInclude(t => t.User)
            .Include(b => b.TutorAvailabilitySlot)
            .ToListAsync();
        var allReviews = await _context.Reviews.ToListAsync();
        var allTutorProfiles = await _context.TutorProfiles.Include(t => t.User).ToListAsync();
        var studentProfilesAll = await _context.StudentProfiles.Include(s => s.User).ToListAsync();
        var allTickets = await _context.SupportTickets.ToListAsync();

        var studentUserIds = studentProfilesAll.Select(s => s.UserId).ToHashSet();
        var tutorUserIds = allTutorProfiles.Select(t => t.UserId).ToHashSet();

        bool InRange(DateTime d, DateTime start, DateTime end) => d >= start && d < end;

        // ---- Overview KPIs ----
        var totalPlatformUsers = allUsers.Count;
        var usersBeforeRange = allUsers.Count(u => u.CreatedAt < rangeStart);
        int? totalUsersTrend = usersBeforeRange == 0 ? null : (int)Math.Round(100.0 * (totalPlatformUsers - usersBeforeRange) / usersBeforeRange);

        var sessionsThisRange = allBookingsFull.Count(b => b.Status != "Cancelled" && InRange(b.TutorAvailabilitySlot.StartTime, rangeStart, rangeEnd));
        var sessionsPrevRange = allBookingsFull.Count(b => b.Status != "Cancelled" && InRange(b.TutorAvailabilitySlot.StartTime, previousRangeStart, previousRangeEnd));
        int? sessionsTrend = sessionsPrevRange == 0 ? null : (int)Math.Round(100.0 * (sessionsThisRange - sessionsPrevRange) / sessionsPrevRange);

        var avgRating = allReviews.Any() ? (decimal)allReviews.Average(r => r.Rating) : 0m;

        var ratingsThisRange = allBookingsFull
            .Where(b => InRange(b.TutorAvailabilitySlot.StartTime, rangeStart, rangeEnd))
            .Join(allReviews, b => b.Id, r => r.BookingId, (b, r) => r.Rating).ToList();
        var ratingsPrevRange = allBookingsFull
            .Where(b => InRange(b.TutorAvailabilitySlot.StartTime, previousRangeStart, previousRangeEnd))
            .Join(allReviews, b => b.Id, r => r.BookingId, (b, r) => r.Rating).ToList();

        string ratingTrendLabel;
        if (!ratingsThisRange.Any() || !ratingsPrevRange.Any())
        {
            ratingTrendLabel = "Stable";
        }
        else
        {
            var diff = ratingsThisRange.Average() - ratingsPrevRange.Average();
            ratingTrendLabel = Math.Abs(diff) < 0.05 ? "Stable" : (diff > 0 ? $"+{diff:0.0}" : $"{diff:0.0}");
        }

        // Retention: students active (non-cancelled booking) in the previous
        // range who also booked again in the current range.
        var studentsActivePrev = allBookingsFull
            .Where(b => b.Status != "Cancelled" && InRange(b.TutorAvailabilitySlot.StartTime, previousRangeStart, previousRangeEnd))
            .Select(b => b.StudentProfileId).Distinct().ToHashSet();
        var studentsActiveThis = allBookingsFull
            .Where(b => b.Status != "Cancelled" && InRange(b.TutorAvailabilitySlot.StartTime, rangeStart, rangeEnd))
            .Select(b => b.StudentProfileId).Distinct().ToHashSet();
        var retainedCount = studentsActivePrev.Count(id => studentsActiveThis.Contains(id));
        var retentionRate = studentsActivePrev.Count == 0 ? 0 : (int)Math.Round(100.0 * retainedCount / studentsActivePrev.Count);

        // ---- User growth chart (last 6 months) ----
        var growthMonthLabels = new List<string>();
        var growthStudentCounts = new List<int>();
        var growthTutorCounts = new List<int>();
        for (var i = 5; i >= 0; i--)
        {
            var monthStart = new DateTime(now.Year, now.Month, 1).AddMonths(-i);
            var monthEnd = monthStart.AddMonths(1);
            growthMonthLabels.Add(monthStart.ToString("MMM"));
            growthStudentCounts.Add(allUsers.Count(u => u.CreatedAt < monthEnd && studentUserIds.Contains(u.Id)));
            growthTutorCounts.Add(allUsers.Count(u => u.CreatedAt < monthEnd && tutorUserIds.Contains(u.Id)));
        }

        // ---- Sessions by subject (donut) ----
        var subjectGroups = allBookingsFull
            .Where(b => b.Status != "Cancelled")
            .GroupBy(b => b.Subject)
            .Select(g => new { Subject = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .ToList();
        var totalSubjectSessions = subjectGroups.Sum(g => g.Count);
        var colorClasses = new[] { "rp-c1", "rp-c2", "rp-c3", "rp-c4", "rp-c5" };
        var subjectShares = subjectGroups.Take(5).Select((g, i) => new SubjectShareViewModel
        {
            Subject = g.Subject,
            Count = g.Count,
            Percent = totalSubjectSessions == 0 ? 0 : (int)Math.Round(100.0 * g.Count / totalSubjectSessions),
            ColorClass = colorClasses[i % colorClasses.Length]
        }).ToList();

        string? fastestGrowingSubject = null;
        int? fastestGrowingPercent = null;
        var subjectGrowthCandidates = subjectGroups.Select(g =>
        {
            var thisQ = allBookingsFull.Count(b => b.Subject == g.Subject && b.Status != "Cancelled" && InRange(b.TutorAvailabilitySlot.StartTime, rangeStart, rangeEnd));
            var prevQ = allBookingsFull.Count(b => b.Subject == g.Subject && b.Status != "Cancelled" && InRange(b.TutorAvailabilitySlot.StartTime, previousRangeStart, previousRangeEnd));
            int? growth = prevQ == 0 ? (thisQ > 0 ? 100 : (int?)null) : (int)Math.Round(100.0 * (thisQ - prevQ) / prevQ);
            return (g.Subject, Growth: growth);
        }).Where(x => x.Growth.HasValue).OrderByDescending(x => x.Growth).FirstOrDefault();
        if (subjectGrowthCandidates.Subject != null)
        {
            fastestGrowingSubject = subjectGrowthCandidates.Subject;
            fastestGrowingPercent = subjectGrowthCandidates.Growth;
        }

        // ---- Tutor performance (used by Overview top-4 and full tab) ----
        var tutorPerf = allTutorProfiles.Where(t => t.IsVerified).Select(t =>
        {
            var tutorBookings = allBookingsFull.Where(b => b.TutorProfileId == t.Id).ToList();
            var completed = tutorBookings.Count(b => b.Status == "Completed");
            var decided = tutorBookings.Count(b => b.Status is "Completed" or "Missed" or "Cancelled");
            var completionPercent = decided == 0 ? 0 : (int)Math.Round(100.0 * completed / decided);

            return new TutorPerformanceRowViewModel
            {
                Name = t.User.FullName,
                Initials = GetInitials(t.User.FullName),
                Subjects = string.Join(", ", t.Subjects.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(2)),
                Sessions = tutorBookings.Count(b => b.Status != "Cancelled"),
                Rating = t.AverageRating,
                CompletionPercent = completionPercent
            };
        })
        .OrderByDescending(t => t.Sessions)
        .ToList();

        // ---- Platform health summary ----
        var totalTutorDecisions = allTutorProfiles.Count(t => t.IsVerified || t.VerificationRejected);
        var approvedTutorDecisions = allTutorProfiles.Count(t => t.IsVerified);
        var tutorApprovalRate = totalTutorDecisions == 0 ? 0 : (int)Math.Round(100.0 * approvedTutorDecisions / totalTutorDecisions);

        var totalDecidedSessions = allBookingsFull.Count(b => b.Status is "Completed" or "Missed");
        var completedSessionsAll = allBookingsFull.Count(b => b.Status == "Completed");
        var sessionCompletionRate = totalDecidedSessions == 0 ? 0 : (int)Math.Round(100.0 * completedSessionsAll / totalDecidedSessions);

        var studentSatisfaction = allReviews.Count == 0 ? 0 : (int)Math.Round(100.0 * allReviews.Count(r => r.Rating >= 4) / allReviews.Count);

        var totalTicketsAll = allTickets.Count;
        var resolvedTicketsAll = allTickets.Count(t => t.Status == "Resolved");
        var complaintResolutionRate = totalTicketsAll == 0 ? 0 : (int)Math.Round(100.0 * resolvedTicketsAll / totalTicketsAll);

        // ---- User growth tab: 12-month view + by district ----
        var growth12Labels = new List<string>();
        var growth12Students = new List<int>();
        var growth12Tutors = new List<int>();
        for (var i = 11; i >= 0; i--)
        {
            var monthStart = new DateTime(now.Year, now.Month, 1).AddMonths(-i);
            var monthEnd = monthStart.AddMonths(1);
            growth12Labels.Add(monthStart.ToString("MMM yy"));
            growth12Students.Add(allUsers.Count(u => u.CreatedAt < monthEnd && studentUserIds.Contains(u.Id)));
            growth12Tutors.Add(allUsers.Count(u => u.CreatedAt < monthEnd && tutorUserIds.Contains(u.Id)));
        }

        var newStudentsThisQuarter = studentProfilesAll.Count(s => InRange(s.User.CreatedAt, rangeStart, rangeEnd));
        var newTutorsThisQuarter = allTutorProfiles.Count(t => InRange(t.User.CreatedAt, rangeStart, rangeEnd));

        var growthByDistrict = studentProfilesAll.Select(s => s.User.District)
            .Concat(allTutorProfiles.Select(t => t.User.District))
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Select(d => d!)
            .Distinct()
            .OrderBy(d => d)
            .Select(d => new DistrictGrowthRowViewModel
            {
                District = d,
                StudentCount = studentProfilesAll.Count(s => s.User.District == d),
                TutorCount = allTutorProfiles.Count(t => t.User.District == d)
            })
            .OrderByDescending(r => r.StudentCount + r.TutorCount)
            .ToList();

        // ---- Subject demand tab ----
        var subjectDemand = subjectGroups.Select(g =>
        {
            var thisQ = allBookingsFull.Count(b => b.Subject == g.Subject && b.Status != "Cancelled" && InRange(b.TutorAvailabilitySlot.StartTime, rangeStart, rangeEnd));
            var prevQ = allBookingsFull.Count(b => b.Subject == g.Subject && b.Status != "Cancelled" && InRange(b.TutorAvailabilitySlot.StartTime, previousRangeStart, previousRangeEnd));
            int? growth = prevQ == 0 ? (thisQ > 0 ? 100 : (int?)null) : (int)Math.Round(100.0 * (thisQ - prevQ) / prevQ);

            var subjectReviewRatings = allBookingsFull
                .Where(b => b.Subject == g.Subject)
                .Join(allReviews, b => b.Id, r => r.BookingId, (b, r) => r.Rating)
                .ToList();

            return new SubjectDemandRowViewModel
            {
                Subject = g.Subject,
                Sessions = g.Count,
                Percent = totalSubjectSessions == 0 ? 0 : (int)Math.Round(100.0 * g.Count / totalSubjectSessions),
                GrowthPercent = growth,
                AvgRating = subjectReviewRatings.Any() ? (decimal)subjectReviewRatings.Average() : 0m
            };
        }).ToList();

        var vm = new AdminReportsViewModel
        {
            AdminName = admin?.FullName ?? "Administrator",
            AdminInitials = GetInitials(admin?.FullName ?? "Administrator"),
            QuarterFilter = quarter,
            QuarterLabel = quarterLabel,
            TotalPlatformUsers = totalPlatformUsers,
            TotalUsersTrendPercent = totalUsersTrend,
            SessionsThisQuarter = sessionsThisRange,
            SessionsTrendPercent = sessionsTrend,
            AvgSessionRating = Math.Round(avgRating, 1),
            RatingTrendLabel = ratingTrendLabel,
            RetentionRatePercent = retentionRate,
            GrowthMonthLabels = growthMonthLabels,
            GrowthStudentCounts = growthStudentCounts,
            GrowthTutorCounts = growthTutorCounts,
            SubjectShares = subjectShares,
            FastestGrowingSubject = fastestGrowingSubject,
            FastestGrowingSubjectPercent = fastestGrowingPercent,
            TotalSubjectSessions = totalSubjectSessions,
            TopTutors = tutorPerf.Take(4).ToList(),
            TutorApprovalRatePercent = tutorApprovalRate,
            SessionCompletionRatePercent = sessionCompletionRate,
            StudentSatisfactionPercent = studentSatisfaction,
            ComplaintResolutionRatePercent = complaintResolutionRate,
            Growth12MonthLabels = growth12Labels,
            Growth12MonthStudents = growth12Students,
            Growth12MonthTutors = growth12Tutors,
            NewStudentsThisQuarter = newStudentsThisQuarter,
            NewTutorsThisQuarter = newTutorsThisQuarter,
            GrowthByDistrict = growthByDistrict,
            AllTutorsPerformance = tutorPerf,
            SubjectDemand = subjectDemand
        };

        return View(vm);
    }
    // ── Complaints ────────────────────────────────────────────────────────

    // Derives who a complaint is "against" from the linked session, since
    // there's no separate "who is this about" field - a Booking-category
    // ticket already has both parties (StudentProfile + TutorProfile on the
    // booking), so whichever side didn't file it is who it's against.
    // Tickets with no BookingId (general "Account"/"Other" support requests)
    // simply have no "against" party - that's expected, not every ticket is
    // a complaint about a specific person.
    private static (string? Name, string? Initials, string? Role, string? Email) ComputeAgainst(SupportTicket t)
    {
        if (t.Booking == null) return (null, null, null, null);

        if (t.StudentProfileId.HasValue)
        {
            var tutorUser = t.Booking.TutorProfile.User;
            return (tutorUser.FullName, GetInitials(tutorUser.FullName), "Tutor", tutorUser.Email);
        }

        if (t.TutorProfileId.HasValue)
        {
            var studentUser = t.Booking.StudentProfile.User;
            return (studentUser.FullName, GetInitials(studentUser.FullName), "Student", studentUser.Email);
        }

        return (null, null, null, null);
    }

    private AdminComplaintCardViewModel BuildComplaintCard(SupportTicket t)
    {
        var isStudentFiler = t.StudentProfileId.HasValue;
        var filerUser = isStudentFiler ? t.StudentProfile!.User : t.TutorProfile!.User;
        var against = ComputeAgainst(t);

        return new AdminComplaintCardViewModel
        {
            Id = t.Id,
            Title = t.Subject,
            Severity = t.Severity,
            Status = t.Status,
            FilerName = filerUser.FullName,
            FilerInitials = GetInitials(filerUser.FullName),
            FilerRole = isStudentFiler ? "Student" : "Tutor",
            FilerEmail = filerUser.Email,
            AgainstName = against.Name,
            AgainstInitials = against.Initials,
            AgainstRole = against.Role,
            AgainstEmail = against.Email,
            SessionCode = t.BookingId.HasValue ? $"SES-{t.BookingId:D4}" : null,
            SessionSubject = t.Booking?.Subject,
            SessionDate = t.Booking?.TutorAvailabilitySlot.StartTime,
            Message = t.Message,
            FiledAt = t.CreatedAt,
            ResolutionNote = t.ResolutionNote,
            ResolvedAt = t.ResolvedAt
        };
    }

    public async Task<IActionResult> Complaints(
        string tab = "open",
        string? search = null,
        string? complaintType = null,
        string? severity = null,
        string dateFiled = "all",
        string sort = "newest",
        int visibleCount = 3)
    {
        await SetAdminNotificationBellAsync();
        var admin = await _userManager.GetUserAsync(User);
        var now = DateTime.Now;
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var previousMonthStart = monthStart.AddMonths(-1);

        var allTickets = await _context.SupportTickets
            .Include(t => t.StudentProfile).ThenInclude(s => s!.User)
            .Include(t => t.TutorProfile).ThenInclude(tp => tp!.User)
            .Include(t => t.Booking).ThenInclude(b => b!.StudentProfile).ThenInclude(s => s.User)
            .Include(t => t.Booking).ThenInclude(b => b!.TutorProfile).ThenInclude(tp => tp.User)
            .Include(t => t.Booking).ThenInclude(b => b!.TutorAvailabilitySlot)
            .ToListAsync();

        // ---- KPI strip ----
        var openCount = allTickets.Count(t => t.Status == "Open");
        var hasUrgentOpen = allTickets.Any(t => t.Status == "Open" && t.Severity == "High");
        var underReviewCount = allTickets.Count(t => t.Status == "UnderReview");

        var resolvedThisMonth = allTickets.Count(t => t.Status == "Resolved" && t.ResolvedAt >= monthStart);
        var resolvedPreviousMonth = allTickets.Count(t =>
            t.Status == "Resolved" && t.ResolvedAt >= previousMonthStart && t.ResolvedAt < monthStart);
        int? resolvedTrend = resolvedPreviousMonth == 0
            ? null
            : (int)Math.Round(100.0 * (resolvedThisMonth - resolvedPreviousMonth) / resolvedPreviousMonth);

        var resolutionDurations = allTickets
            .Where(t => t.Status == "Resolved" && t.ResolvedAt.HasValue)
            .Select(t => (t.ResolvedAt!.Value - t.CreatedAt).TotalDays)
            .Where(d => d >= 0)
            .ToList();
        double? avgResolutionDays = resolutionDurations.Count == 0 ? null : Math.Round(resolutionDurations.Average(), 1);

        var resolvedAllTime = allTickets.Count(t => t.Status == "Resolved");
        var resolutionRate = allTickets.Count == 0 ? 0 : (int)Math.Round(100.0 * resolvedAllTime / allTickets.Count);

        // ---- Tab filter ----
        IEnumerable<SupportTicket> filtered = tab switch
        {
            "underreview" => allTickets.Where(t => t.Status == "UnderReview"),
            "resolved" => allTickets.Where(t => t.Status == "Resolved"),
            "all" => allTickets,
            _ => allTickets.Where(t => t.Status == "Open")
        };

        // ---- Explicit filters ----
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            filtered = filtered.Where(t =>
                t.Subject.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                $"CMP-{t.Id:D4}".Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (t.StudentProfile != null && t.StudentProfile.User.FullName.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (t.TutorProfile != null && t.TutorProfile.User.FullName.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (t.BookingId.HasValue && $"SES-{t.BookingId:D4}".Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(complaintType))
            filtered = filtered.Where(t => t.Subject == complaintType);

        if (!string.IsNullOrWhiteSpace(severity))
            filtered = filtered.Where(t => t.Severity == severity);

        filtered = dateFiled switch
        {
            "last7" => filtered.Where(t => t.CreatedAt >= now.AddDays(-7)),
            "last30" => filtered.Where(t => t.CreatedAt >= now.AddDays(-30)),
            "last90" => filtered.Where(t => t.CreatedAt >= now.AddDays(-90)),
            _ => filtered
        };

        var sorted = sort switch
        {
            "oldest" => filtered.OrderBy(t => t.CreatedAt),
            "severity" => filtered.OrderByDescending(t => t.Severity == "High" ? 2 : t.Severity == "Medium" ? 1 : 0),
            _ => filtered.OrderByDescending(t => t.CreatedAt) // "newest"
        };

        var sortedList = sorted.ToList();
        var totalMatching = sortedList.Count;

        visibleCount = Math.Max(3, visibleCount);
        var pageTickets = sortedList.Take(visibleCount).ToList();
        var cards = pageTickets.Select(BuildComplaintCard).ToList();

        // ---- Secondary preview sections (Open tab only) ----
        var underReviewPreview = tab == "open"
            ? allTickets.Where(t => t.Status == "UnderReview").OrderByDescending(t => t.CreatedAt).Take(3)
                .Select(t => new AdminComplaintTableRowViewModel
                {
                    Id = t.Id,
                    Title = t.Subject,
                    FilerName = (t.StudentProfileId.HasValue ? t.StudentProfile!.User.FullName : t.TutorProfile!.User.FullName),
                    AgainstName = ComputeAgainst(t).Name,
                    FiledAt = t.CreatedAt,
                    Severity = t.Severity,
                    Status = t.Status
                }).ToList()
            : new List<AdminComplaintTableRowViewModel>();

        var resolvedPreview = tab == "open"
            ? allTickets.Where(t => t.Status == "Resolved").OrderByDescending(t => t.ResolvedAt).Take(2)
                .Select(t => new AdminComplaintTableRowViewModel
                {
                    Id = t.Id,
                    Title = t.Subject,
                    FilerName = (t.StudentProfileId.HasValue ? t.StudentProfile!.User.FullName : t.TutorProfile!.User.FullName),
                    AgainstName = ComputeAgainst(t).Name,
                    FiledAt = t.CreatedAt,
                    Severity = t.Severity,
                    Status = t.Status,
                    ResolutionNote = t.ResolutionNote,
                    ResolvedAt = t.ResolvedAt
                }).ToList()
            : new List<AdminComplaintTableRowViewModel>();

        var complaintTypeOptions = allTickets.Select(t => t.Subject).Distinct().OrderBy(s => s).ToList();

        var vm = new AdminComplaintsViewModel
        {
            AdminName = admin?.FullName ?? "Administrator",
            AdminInitials = GetInitials(admin?.FullName ?? "Administrator"),
            OpenCount = openCount,
            HasUrgentOpen = hasUrgentOpen,
            UnderReviewCount = underReviewCount,
            MonthLabel = now.ToString("MMMM"),
            ResolvedThisMonth = resolvedThisMonth,
            ResolvedTrendPercent = resolvedTrend,
            AvgResolutionDays = avgResolutionDays,
            ResolutionRatePercent = resolutionRate,
            ActiveTab = tab,
            OpenTabCount = openCount,
            UnderReviewTabCount = underReviewCount,
            ResolvedTabCount = resolvedAllTime,
            AllTabCount = allTickets.Count,
            Search = search,
            ComplaintType = complaintType,
            SeverityFilter = severity,
            DateFiledFilter = dateFiled,
            Sort = sort,
            ComplaintTypes = complaintTypeOptions,
            Cards = cards,
            VisibleCount = visibleCount,
            TotalMatching = totalMatching,
            UnderReviewPreview = underReviewPreview,
            ResolvedPreview = resolvedPreview
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> InvestigateComplaint(int complaintId, string? returnUrl)
    {
        var ticket = await _context.SupportTickets.FirstOrDefaultAsync(t => t.Id == complaintId);
        if (ticket != null && ticket.Status == "Open")
        {
            ticket.Status = "UnderReview";
            await _context.SaveChangesAsync();
        }

        return string.IsNullOrWhiteSpace(returnUrl) ? RedirectToAction("Complaints") : LocalRedirect(returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResolveComplaint(int complaintId, string resolutionNote, string? returnUrl)
    {
        var ticket = await _context.SupportTickets
            .Include(t => t.StudentProfile).ThenInclude(s => s!.User)
            .Include(t => t.TutorProfile).ThenInclude(tp => tp!.User)
            .FirstOrDefaultAsync(t => t.Id == complaintId);
        if (ticket != null && !string.IsNullOrWhiteSpace(resolutionNote))
        {
            ticket.Status = "Resolved";
            ticket.ResolutionNote = resolutionNote.Trim();
            ticket.ResolvedAt = DateTime.Now;

            if (ticket.BookingId.HasValue)
            {
                var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.Id == ticket.BookingId.Value);
                if (booking != null) booking.IsDisputed = false;
            }

            var filerName = ticket.StudentProfileId.HasValue ? ticket.StudentProfile!.User.FullName : ticket.TutorProfile!.User.FullName;
            NotificationHelper.Create(_context,
                type: "Complaint",
                title: "Complaint resolved",
                message: $"{ticket.Subject} filed by {filerName} — {resolutionNote.Trim()}",
                icon: "📋");

            await _context.SaveChangesAsync();
        }

        return string.IsNullOrWhiteSpace(returnUrl) ? RedirectToAction("Complaints") : LocalRedirect(returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetComplaintSeverity(int complaintId, string severity, string? returnUrl)
    {
        var validSeverities = new[] { "High", "Medium", "Low" };
        var ticket = await _context.SupportTickets.FirstOrDefaultAsync(t => t.Id == complaintId);
        if (ticket != null && validSeverities.Contains(severity))
        {
            ticket.Severity = severity;
            await _context.SaveChangesAsync();
        }

        return string.IsNullOrWhiteSpace(returnUrl) ? RedirectToAction("Complaints") : LocalRedirect(returnUrl);
    }

    public async Task<IActionResult> ComplaintDetail(int id, string? returnUrl)
    {
        await SetAdminNotificationBellAsync();

        var ticket = await _context.SupportTickets
            .Include(t => t.StudentProfile).ThenInclude(s => s!.User)
            .Include(t => t.TutorProfile).ThenInclude(tp => tp!.User)
            .Include(t => t.Booking).ThenInclude(b => b!.StudentProfile).ThenInclude(s => s.User)
            .Include(t => t.Booking).ThenInclude(b => b!.TutorProfile).ThenInclude(tp => tp.User)
            .Include(t => t.Booking).ThenInclude(b => b!.TutorAvailabilitySlot)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (ticket == null) return NotFound();

        ViewData["ReturnUrl"] = returnUrl;
        return View(BuildComplaintCard(ticket));
    }

    public async Task<IActionResult> ExportComplaintsCsv(
        string tab = "all",
        string? search = null,
        string? complaintType = null,
        string? severity = null,
        string dateFiled = "all")
    {
        var allTickets = await _context.SupportTickets
            .Include(t => t.StudentProfile).ThenInclude(s => s!.User)
            .Include(t => t.TutorProfile).ThenInclude(tp => tp!.User)
            .ToListAsync();
        var now = DateTime.Now;

        IEnumerable<SupportTicket> filtered = tab switch
        {
            "underreview" => allTickets.Where(t => t.Status == "UnderReview"),
            "resolved" => allTickets.Where(t => t.Status == "Resolved"),
            "open" => allTickets.Where(t => t.Status == "Open"),
            _ => allTickets
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            filtered = filtered.Where(t => t.Subject.Contains(term, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(complaintType))
            filtered = filtered.Where(t => t.Subject == complaintType);
        if (!string.IsNullOrWhiteSpace(severity))
            filtered = filtered.Where(t => t.Severity == severity);
        filtered = dateFiled switch
        {
            "last7" => filtered.Where(t => t.CreatedAt >= now.AddDays(-7)),
            "last30" => filtered.Where(t => t.CreatedAt >= now.AddDays(-30)),
            "last90" => filtered.Where(t => t.CreatedAt >= now.AddDays(-90)),
            _ => filtered
        };

        var csv = new System.Text.StringBuilder();
        csv.AppendLine("ComplaintId,Title,FiledBy,Severity,Status,DateFiled,ResolvedOn,Resolution");
        foreach (var t in filtered.OrderByDescending(t => t.CreatedAt))
        {
            string Esc(string? v) => "\"" + (v ?? "").Replace("\"", "\"\"") + "\"";
            var filerName = t.StudentProfileId.HasValue ? t.StudentProfile!.User.FullName : t.TutorProfile!.User.FullName;
            csv.AppendLine(string.Join(",",
                Esc($"CMP-{t.Id:D4}"), Esc(t.Subject), Esc(filerName), Esc(t.Severity), Esc(t.Status),
                Esc(t.CreatedAt.ToString("yyyy-MM-dd")), Esc(t.ResolvedAt?.ToString("yyyy-MM-dd") ?? ""), Esc(t.ResolutionNote)));
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
        return File(bytes, "text/csv", $"tutorbridge-complaints-{DateTime.Now:yyyyMMdd-HHmm}.csv");
    }

    // ── Notifications ──────────────────────────────────────────────────────

    public async Task<IActionResult> Notifications(
        string tab = "all",
        string? type = null,
        string sort = "newest",
        int visibleCount = 10)
    {
        await SetAdminNotificationBellAsync();
        var admin = await _userManager.GetUserAsync(User);
        var now = DateTime.Now;
        var today = now.Date;
        var weekStart = today.AddDays(-(int)today.DayOfWeek + (today.DayOfWeek == DayOfWeek.Sunday ? -6 : 1));

        var allNotifications = await _context.Notifications.ToListAsync();

        var unreadCount = allNotifications.Count(n => !n.IsRead);
        var requiresActionCount = allNotifications.Count(n => !n.IsRead && !string.IsNullOrEmpty(n.ActionUrl));
        var todayCount = allNotifications.Count(n => n.CreatedAt.Date == today);
        var thisWeekCount = allNotifications.Count(n => n.CreatedAt.Date >= weekStart);

        IEnumerable<Notification> filtered = tab switch
        {
            "verifications" => allNotifications.Where(n => n.Type == "Verification"),
            "complaints" => allNotifications.Where(n => n.Type == "Complaint"),
            "system" => allNotifications.Where(n => n.Type == "System"),
            "read" => allNotifications.Where(n => n.IsRead),
            _ => allNotifications
        };

        if (!string.IsNullOrWhiteSpace(type))
            filtered = filtered.Where(n => n.Type == type);

        var sorted = sort == "oldest" ? filtered.OrderBy(n => n.CreatedAt) : filtered.OrderByDescending(n => n.CreatedAt);
        var sortedList = sorted.ToList();
        var totalMatching = sortedList.Count;

        visibleCount = Math.Max(10, visibleCount);
        var pageItems = sortedList.Take(visibleCount).ToList();

        var groups = pageItems
            .GroupBy(n => n.CreatedAt.Date)
            .OrderByDescending(g => g.Key)
            .Select(g => new NotificationDayGroupViewModel
            {
                DayLabel = g.Key == today ? "Today" : g.Key == today.AddDays(-1) ? "Yesterday" : g.Key.ToString("d MMMM yyyy"),
                Items = g.Select(n => new NotificationRowViewModel
                {
                    Id = n.Id,
                    Type = n.Type,
                    Title = n.Title,
                    Message = n.Message,
                    Icon = n.Icon,
                    ActionLabel = n.ActionLabel,
                    ActionUrl = n.ActionUrl,
                    IsRead = n.IsRead,
                    IsHighPriority = n.IsHighPriority,
                    CreatedAt = n.CreatedAt
                }).ToList()
            }).ToList();

        var vm = new AdminNotificationsViewModel
        {
            AdminName = admin?.FullName ?? "Administrator",
            AdminInitials = GetInitials(admin?.FullName ?? "Administrator"),
            UnreadCount = unreadCount,
            RequiresActionCount = requiresActionCount,
            TodayCount = todayCount,
            ThisWeekCount = thisWeekCount,
            ActiveTab = tab,
            TypeFilter = type,
            Sort = sort,
            Groups = groups,
            VisibleCount = visibleCount,
            TotalMatching = totalMatching
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkNotificationRead(int notificationId, string? returnUrl)
    {
        var notification = await _context.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId);
        if (notification != null)
        {
            notification.IsRead = true;
            await _context.SaveChangesAsync();
        }

        return string.IsNullOrWhiteSpace(returnUrl) ? RedirectToAction("Notifications") : LocalRedirect(returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllNotificationsRead(string? returnUrl)
    {
        var unread = await _context.Notifications.Where(n => !n.IsRead).ToListAsync();
        foreach (var n in unread)
        {
            n.IsRead = true;
        }
        await _context.SaveChangesAsync();

        return string.IsNullOrWhiteSpace(returnUrl) ? RedirectToAction("Notifications") : LocalRedirect(returnUrl);
    }

    // ── Settings ───────────────────────────────────────────────────────────

    public async Task<IActionResult> Settings()
    {
        await SetAdminNotificationBellAsync();
        var admin = await _userManager.GetUserAsync(User);
        if (admin == null) return RedirectToAction("AdminLogin", "Account");

        var platformSettings = await GetOrCreatePlatformSettingsAsync();
        var adminCount = (await _userManager.GetUsersInRoleAsync("Admin")).Count;

        var vm = new AdminSettingsPageViewModel
        {
            AdminName = admin.FullName,
            Initials = GetInitials(admin.FullName),
            PhotoUrl = admin.PhotoUrl,
            Profile = new AdminProfileFormModel
            {
                FullName = admin.FullName,
                Email = admin.Email ?? "",
                PhoneNumber = admin.PhoneNumber
            },
            TwoFactorEnabled = admin.TwoFactorEnabled,
            PlatformConfig = new AdminPlatformConfigModel
            {
                AutoApproveVerifiedTutors = platformSettings.AutoApproveVerifiedTutors,
                RequirePoliceReportForTutors = platformSettings.RequirePoliceReportForTutors,
                AllowSameDayBooking = platformSettings.AllowSameDayBooking,
                PlatformMaintenanceMode = platformSettings.PlatformMaintenanceMode
            },
            AdminCount = adminCount
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAdminProfile(AdminProfileFormModel model)
    {
        var admin = await _userManager.GetUserAsync(User);
        if (admin == null) return RedirectToAction("AdminLogin", "Account");

        if (string.IsNullOrWhiteSpace(model.FullName) || string.IsNullOrWhiteSpace(model.Email))
        {
            TempData["SettingsError"] = "Full name and email are required.";
            return RedirectToAction("Settings");
        }

        admin.FullName = model.FullName.Trim();
        admin.PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim();

        if (!string.Equals(admin.Email, model.Email, StringComparison.OrdinalIgnoreCase))
        {
            var setEmailResult = await _userManager.SetEmailAsync(admin, model.Email.Trim());
            if (!setEmailResult.Succeeded)
            {
                TempData["SettingsError"] = "Could not update email: " + string.Join(" ", setEmailResult.Errors.Select(e => e.Description));
                return RedirectToAction("Settings");
            }
            await _userManager.SetUserNameAsync(admin, model.Email.Trim());
        }

        var updateResult = await _userManager.UpdateAsync(admin);
        if (!updateResult.Succeeded)
        {
            TempData["SettingsError"] = "Could not save profile changes.";
            return RedirectToAction("Settings");
        }

        await _signInManager.RefreshSignInAsync(admin);
        TempData["SettingsSuccess"] = "Profile updated.";
        return RedirectToAction("Settings");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadAdminPhoto(IFormFile? photo)
    {
        var admin = await _userManager.GetUserAsync(User);
        if (admin == null) return RedirectToAction("AdminLogin", "Account");

        var (url, error) = await FileUploadHelper.SavePhotoAsync(photo, _webHostEnvironment.WebRootPath);
        if (error != null)
        {
            TempData["SettingsError"] = error;
            return RedirectToAction("Settings");
        }

        FileUploadHelper.TryDelete(admin.PhotoUrl, _webHostEnvironment.WebRootPath);
        admin.PhotoUrl = url;
        await _context.SaveChangesAsync();

        TempData["SettingsSuccess"] = "Profile photo updated.";
        return RedirectToAction("Settings");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveAdminPhoto()
    {
        var admin = await _userManager.GetUserAsync(User);
        if (admin == null) return RedirectToAction("AdminLogin", "Account");

        FileUploadHelper.TryDelete(admin.PhotoUrl, _webHostEnvironment.WebRootPath);
        admin.PhotoUrl = null;
        await _context.SaveChangesAsync();

        TempData["SettingsSuccess"] = "Profile photo removed.";
        return RedirectToAction("Settings");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdminChangePassword(string currentPassword, string newPassword, string confirmNewPassword)
    {
        var admin = await _userManager.GetUserAsync(User);
        if (admin == null) return RedirectToAction("AdminLogin", "Account");

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword != confirmNewPassword)
        {
            TempData["SettingsError"] = "New password and confirmation do not match.";
            return RedirectToAction("Settings");
        }

        var result = await _userManager.ChangePasswordAsync(admin, currentPassword, newPassword);
        if (!result.Succeeded)
        {
            TempData["SettingsError"] = string.Join(" ", result.Errors.Select(e => e.Description));
            return RedirectToAction("Settings");
        }

        await _signInManager.RefreshSignInAsync(admin);
        TempData["SettingsSuccess"] = "Password changed.";
        return RedirectToAction("Settings");
    }

    // Generic toggle handler for the platform configuration switches.
    // "Require police report" and "Maintenance mode" take effect
    // immediately (see ApproveTutor and the Program.cs middleware); the
    // other two are simply persisted for now - see PlatformSettings.cs.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePlatformSetting(string key, bool value)
    {
        var platformSettings = await GetOrCreatePlatformSettingsAsync();

        switch (key)
        {
            case "AutoApproveVerifiedTutors": platformSettings.AutoApproveVerifiedTutors = value; break;
            case "RequirePoliceReportForTutors": platformSettings.RequirePoliceReportForTutors = value; break;
            case "AllowSameDayBooking": platformSettings.AllowSameDayBooking = value; break;
            case "PlatformMaintenanceMode": platformSettings.PlatformMaintenanceMode = value; break;
            default: return RedirectToAction("Settings");
        }

        platformSettings.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync();

        return RedirectToAction("Settings");
    }

    // Sole-admin safeguard shared by DeactivateAdminAccount and
    // DeleteAdminAccount - refuses to let the last remaining Admin lock the
    // platform out of its own console.
    private async Task<bool> IsSoleAdminAsync()
    {
        var admins = await _userManager.GetUsersInRoleAsync("Admin");
        return admins.Count <= 1;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateAdminAccount(string confirmText)
    {
        var admin = await _userManager.GetUserAsync(User);
        if (admin == null) return RedirectToAction("AdminLogin", "Account");

        if (await IsSoleAdminAsync())
        {
            TempData["SettingsError"] = "You're the only admin account - add another admin before deactivating this one.";
            return RedirectToAction("Settings");
        }

        if (!string.Equals(confirmText?.Trim(), "DEACTIVATE", StringComparison.Ordinal))
        {
            TempData["SettingsError"] = "Type DEACTIVATE exactly to confirm.";
            return RedirectToAction("Settings");
        }

        admin.IsSuspended = true;
        await _context.SaveChangesAsync();
        await _signInManager.SignOutAsync();

        return RedirectToAction("AdminLogin", "Account");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAdminAccount(string confirmText)
    {
        var admin = await _userManager.GetUserAsync(User);
        if (admin == null) return RedirectToAction("AdminLogin", "Account");

        if (await IsSoleAdminAsync())
        {
            TempData["SettingsError"] = "You're the only admin account - add another admin before deleting this one.";
            return RedirectToAction("Settings");
        }

        if (!string.Equals(confirmText?.Trim(), "DELETE", StringComparison.Ordinal))
        {
            TempData["SettingsError"] = "Type DELETE exactly to confirm account deletion.";
            return RedirectToAction("Settings");
        }

        await _signInManager.SignOutAsync();
        await _userManager.DeleteAsync(admin);

        return RedirectToAction("Index", "Home");
    }
}