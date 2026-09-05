using Microsoft.EntityFrameworkCore;
using TutorBridgeNepal.Data;
using TutorBridgeNepal.Helpers;

namespace TutorBridgeNepal.Services;

// Runs in the background for the lifetime of the app (no cron/task-scheduler
// dependency needed) and does two things every minute:
//
//   1. Sends the "your session starts in 15 minutes" reminder email to both
//      the student and the tutor for any Confirmed booking that has just
//      entered the join window, exactly once per booking (tracked via
//      Booking.ReminderSentAt).
//
//   2. Sweeps every Confirmed booking whose slot has already ended and flips
//      it to "Missed". SessionStatusHelper.AutoMarkMissed already does this
//      lazily whenever a dashboard/session list loads, but that only covers
//      bookings someone happens to look at. Running it here too makes "no
//      one joined -> Missed" genuinely automatic instead of depending on
//      either party opening the app again.
public class SessionReminderService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SessionReminderService> _logger;

    public SessionReminderService(IServiceScopeFactory scopeFactory, ILogger<SessionReminderService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Small startup delay so this doesn't race the app's own DB
        // migrations/warm-up on first boot.
        try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); } catch (TaskCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SessionReminderService pass failed");
            }

            try { await Task.Delay(PollInterval, stoppingToken); } catch (TaskCanceledException) { break; }
        }
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>() as SmtpEmailSender;

        await SendUpcomingRemindersAsync(context, emailSender, stoppingToken);
        await AutoMarkMissedAsync(context, stoppingToken);
    }

    private async Task SendUpcomingRemindersAsync(ApplicationDbContext context, SmtpEmailSender? emailSender, CancellationToken stoppingToken)
    {
        var now = DateTime.Now;
        var windowStart = now.AddMinutes(SessionStatusHelper.JoinWindowMinutesBeforeStart);

        // Sessions that are about to enter (or have just entered) the join
        // window but haven't started yet, and haven't had a reminder sent.
        var dueBookings = await context.Bookings
            .Include(b => b.TutorAvailabilitySlot)
            .Include(b => b.StudentProfile).ThenInclude(s => s.User)
            .Include(b => b.TutorProfile).ThenInclude(t => t.User)
            .Where(b => b.Status == "Confirmed"
                && b.ReminderSentAt == null
                && b.TutorAvailabilitySlot.StartTime > now
                && b.TutorAvailabilitySlot.StartTime <= windowStart)
            .ToListAsync(stoppingToken);

        foreach (var booking in dueBookings)
        {
            var start = booking.TutorAvailabilitySlot.StartTime;
            var whenLabel = start.ToString("dddd, d MMM yyyy, h:mm tt");
            var studentUser = booking.StudentProfile.User;
            var tutorUser = booking.TutorProfile.User;

            await TrySendAsync(emailSender, studentUser?.Email, "Your session starts in 15 minutes", $@"
                <p>Hi {System.Net.WebUtility.HtmlEncode(studentUser?.FullName ?? "there")},</p>
                <p>Your {System.Net.WebUtility.HtmlEncode(booking.Subject)} session with {System.Net.WebUtility.HtmlEncode(tutorUser?.FullName ?? "your tutor")} starts at {whenLabel} - just 15 minutes from now.</p>
                <p>The Join button is live on your Sessions page whenever you're ready.</p>
                <p>— TutorBridge Nepal</p>");

            await TrySendAsync(emailSender, tutorUser?.Email, "Your session starts in 15 minutes", $@"
                <p>Hi {System.Net.WebUtility.HtmlEncode(tutorUser?.FullName ?? "there")},</p>
                <p>Your {System.Net.WebUtility.HtmlEncode(booking.Subject)} session with {System.Net.WebUtility.HtmlEncode(studentUser?.FullName ?? "your student")} starts at {whenLabel} - just 15 minutes from now.</p>
                <p>The Join button is live on your Dashboard whenever you're ready.</p>
                <p>— TutorBridge Nepal</p>");

            booking.ReminderSentAt = now;
        }

        if (dueBookings.Count > 0)
        {
            await context.SaveChangesAsync(stoppingToken);
        }
    }

    private async Task AutoMarkMissedAsync(ApplicationDbContext context, CancellationToken stoppingToken)
    {
        var now = DateTime.Now;

        var possiblyMissed = await context.Bookings
            .Include(b => b.TutorAvailabilitySlot)
            .Where(b => b.Status == "Confirmed" && b.TutorAvailabilitySlot.EndTime <= now)
            .ToListAsync(stoppingToken);

        if (possiblyMissed.Count == 0) return;

        if (SessionStatusHelper.AutoMarkMissed(possiblyMissed))
        {
            await context.SaveChangesAsync(stoppingToken);
        }
    }

    // Best-effort, matching the pattern already used in StudentController/
    // TutorController: a reminder email failing to send should never crash
    // the background loop or block the next booking from being processed.
    private async Task TrySendAsync(SmtpEmailSender? sender, string? toEmail, string subject, string bodyHtml)
    {
        if (sender == null || !sender.IsConfigured) return;
        if (string.IsNullOrWhiteSpace(toEmail)) return;

        try
        {
            await sender.SendEmailAsync(toEmail, subject, bodyHtml);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send session reminder email to {Email}", toEmail);
        }
    }
}