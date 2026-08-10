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
    private readonly IWebHostEnvironment _webHostEnvironment;

    public AdminController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IWebHostEnvironment webHostEnvironment)
    {
        _context = context;
        _userManager = userManager;
        _webHostEnvironment = webHostEnvironment;
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
            .Where(t => t.Status == "Open")
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
        var studentSatisfaction = allRatings.Count == 0 ? 0 : (int)Math.Round(20.0 * allRatings.Average());

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
                VerificationNote = t.VerificationNote
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
        if (selectedUserIds != null && selectedUserIds.Count > 0 && (bulkAction == "activate" || bulkAction == "suspend"))
        {
            var users = await _context.Users.Where(u => selectedUserIds.Contains(u.Id)).ToListAsync();
            foreach (var user in users)
            {
                user.IsSuspended = bulkAction == "suspend";
            }
            await _context.SaveChangesAsync();
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
        var tutor = await _context.TutorProfiles.FirstOrDefaultAsync(t => t.Id == tutorProfileId);
        if (tutor != null)
        {
            tutor.IsVerified = true;
            tutor.VerificationRejected = false;
            tutor.VerificationDecidedAt = DateTime.Now;
            await _context.SaveChangesAsync();
        }

        return string.IsNullOrWhiteSpace(returnUrl) ? RedirectToAction("Dashboard") : LocalRedirect(returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectTutor(int tutorProfileId, string reason, string? returnUrl)
    {
        var tutor = await _context.TutorProfiles.FirstOrDefaultAsync(t => t.Id == tutorProfileId);
        if (tutor != null)
        {
            tutor.IsVerified = false;
            tutor.VerificationRejected = true;
            tutor.VerificationDecidedAt = DateTime.Now;
            tutor.VerificationNote = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            await _context.SaveChangesAsync();
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
        var tutor = await _context.TutorProfiles.FirstOrDefaultAsync(t => t.Id == tutorProfileId);
        if (tutor != null && !string.IsNullOrWhiteSpace(note))
        {
            tutor.VerificationNote = note.Trim();
            await _context.SaveChangesAsync();
        }

        return string.IsNullOrWhiteSpace(returnUrl) ? RedirectToAction("TutorVerification") : LocalRedirect(returnUrl);
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
                .Where(t => t.BookingId == bookingId && t.Status == "Open")
                .ToListAsync();
            foreach (var ticket in openTickets)
            {
                ticket.Status = "Resolved";
            }

            await _context.SaveChangesAsync();
        }

        return string.IsNullOrWhiteSpace(returnUrl) ? RedirectToAction("SessionLogs") : LocalRedirect(returnUrl);
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
}