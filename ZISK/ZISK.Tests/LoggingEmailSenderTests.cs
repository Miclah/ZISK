using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZISK.Services;

namespace ZISK.Tests;

public class LoggingEmailSenderTests
{
    [Fact]
    public async Task SendEmailAsync_NeverAttemptsSmtpConnection()
    {
        // Deliberately unreachable host/port - if LoggingEmailSender ever tried to actually
        // connect (e.g. a future refactor accidentally calls the base implementation),
        // this would time out or throw instead of completing immediately.
        var badSettings = Options.Create(new SmtpSettings
        {
            Host = "smtp.invalid.example",
            Port = 1,
            SenderEmail = "demo@zisk.sk",
            SenderName = "ZISK Demo"
        });

        var sender = new LoggingEmailSender(badSettings, new LoggerFactory().CreateLogger<LoggingEmailSender>());

        await sender.SendEmailAsync("someone@test.sk", "Subject", "<p>Body</p>");
        // No exception and no hang means the email was suppressed rather than actually sent.
    }
}
