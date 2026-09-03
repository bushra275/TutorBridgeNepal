using System.Net;
using System.Net.Mail;

namespace TutorBridgeNepal.Services;

// Real SMTP-based email sending, configured via the "EmailSettings" section
// in appsettings.json (or appsettings.Development.json, which should be
// git-ignored so real credentials never get committed).
public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _config;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IConfiguration config, ILogger<SmtpEmailSender> logger)
    {
        _config = config;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_config["EmailSettings:Host"]) &&
        !string.IsNullOrWhiteSpace(_config["EmailSettings:Username"]) &&
        !string.IsNullOrWhiteSpace(_config["EmailSettings:Password"]);

    public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        var host = _config["EmailSettings:Host"];
        var port = int.Parse(_config["EmailSettings:Port"] ?? "587");
        var username = _config["EmailSettings:Username"];
        var password = _config["EmailSettings:Password"];
        var fromEmail = _config["EmailSettings:FromEmail"] ?? username;
        var fromName = _config["EmailSettings:FromName"] ?? "TutorBridge Nepal";
        var enableSsl = bool.Parse(_config["EmailSettings:EnableSsl"] ?? "true");

        using var client = new SmtpClient(host, port)
        {
            Credentials = new NetworkCredential(username, password),
            EnableSsl = enableSsl
        };

        using var message = new MailMessage
        {
            From = new MailAddress(fromEmail!, fromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(toEmail);

        try
        {
            await client.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
            throw;
        }
    }
}