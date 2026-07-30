using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ZISK.Services;

/// <summary>
/// Used instead of <see cref="SmtpEmailSender"/> in demo mode (ZISK_SEED_MODE=demo) - logs the
/// outgoing email instead of actually sending it. A public demo deployment has no business
/// emailing arbitrary addresses on someone else's behalf, and doesn't need real SMTP
/// credentials sitting in its configuration.
/// </summary>
public class LoggingEmailSender : SmtpEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(IOptions<SmtpSettings> smtp, ILogger<LoggingEmailSender> logger)
        : base(smtp, NullLogger<SmtpEmailSender>.Instance)
    {
        _logger = logger;
    }

    public override Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        _logger.LogInformation("[DEMO] Email suppressed - would send to {Email}: {Subject}", email, subject);
        return Task.CompletedTask;
    }
}
