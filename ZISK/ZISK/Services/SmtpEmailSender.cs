using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using MimeKit;
using ZISK.Data;

namespace ZISK.Services;

public class SmtpEmailSender : IEmailSender<ApplicationUser>, IEmailSender
{
    private readonly SmtpSettings _smtp;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<SmtpSettings> smtp, ILogger<SmtpEmailSender> logger)
    {
        _smtp = smtp.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_smtp.SenderName, _smtp.SenderEmail));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = subject;

        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = WrapInTemplate(subject, htmlMessage)
        };
        message.Body = bodyBuilder.ToMessageBody();

        try
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(_smtp.Host, _smtp.Port,
                _smtp.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None);

            if (!string.IsNullOrEmpty(_smtp.Username))
            {
                await client.AuthenticateAsync(_smtp.Username, _smtp.Password);
            }

            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Email sent to {Email}: {Subject}", email, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}: {Subject}", email, subject);
            throw;
        }
    }

    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
    {
        var html = $"""
            <h2>Vitajte v ZISK, {user.FirstName}!</h2>
            <p>Ďakujeme za registráciu. Pre aktiváciu vášho účtu potvrďte svoju emailovú adresu kliknutím na tlačidlo nižšie:</p>
            <p style="text-align: center; margin: 30px 0;">
                <a href="{confirmationLink}" 
                   style="background-color: #1976d2; color: white; padding: 12px 30px; 
                          text-decoration: none; border-radius: 6px; font-weight: bold;
                          display: inline-block;">
                    Potvrdiť email
                </a>
            </p>
            <p style="color: #666; font-size: 13px;">
                Ak tlačidlo nefunguje, skopírujte tento odkaz do prehliadača:<br/>
                <a href="{confirmationLink}" style="color: #1976d2;">{confirmationLink}</a>
            </p>
            """;

        return SendEmailAsync(email, "ZISK – Potvrdenie emailovej adresy", html);
    }

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
    {
        var html = $"""
            <h2>Obnovenie hesla</h2>
            <p>Dostali sme žiadosť o obnovenie hesla pre váš účet. Kliknite na tlačidlo nižšie:</p>
            <p style="text-align: center; margin: 30px 0;">
                <a href="{resetLink}" 
                   style="background-color: #f57c00; color: white; padding: 12px 30px; 
                          text-decoration: none; border-radius: 6px; font-weight: bold;
                          display: inline-block;">
                    Obnoviť heslo
                </a>
            </p>
            <p style="color: #666; font-size: 13px;">Ak ste o obnovenie hesla nežiadali, tento email ignorujte.</p>
            """;

        return SendEmailAsync(email, "ZISK – Obnovenie hesla", html);
    }

    public Task SendEmailChangeLinkAsync(ApplicationUser user, string newEmail, string changeLink)
    {
        var html = $"""
            <h2>Zmena emailovej adresy</h2>
            <p>Dostali sme žiadosť o zmenu emailovej adresy vášho účtu na <strong>{newEmail}</strong>. Kliknite na tlačidlo nižšie na potvrdenie:</p>
            <p style="text-align: center; margin: 30px 0;">
                <a href="{changeLink}"
                   style="background-color: #1976d2; color: white; padding: 12px 30px;
                          text-decoration: none; border-radius: 6px; font-weight: bold;
                          display: inline-block;">
                    Potvrdiť zmenu emailu
                </a>
            </p>
            <p style="color: #666; font-size: 13px;">
                Ak ste o zmenu emailu nežiadali, tento email ignorujte.<br/>
                Ak tlačidlo nefunguje, skopírujte tento odkaz do prehliadača:<br/>
                <a href="{changeLink}" style="color: #1976d2;">{changeLink}</a>
            </p>
            """;

        return SendEmailAsync(newEmail, "ZISK – Potvrdenie zmeny emailovej adresy", html);
    }

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
    {
        var html = $"""
            <h2>Kód pre obnovenie hesla</h2>
            <p>Váš kód pre obnovenie hesla:</p>
            <p style="text-align: center; margin: 30px 0; font-size: 32px; font-weight: bold; 
                      letter-spacing: 8px; color: #1976d2;">
                {resetCode}
            </p>
            <p style="color: #666; font-size: 13px;">Ak ste o obnovenie hesla nežiadali, tento email ignorujte.</p>
            """;

        return SendEmailAsync(email, "ZISK – Kód pre obnovenie hesla", html);
    }

    private static string WrapInTemplate(string title, string content)
    {
        return $"""
            <!DOCTYPE html>
            <html>
            <head><meta charset="utf-8" /></head>
            <body style="font-family: 'Segoe UI', Arial, sans-serif; max-width: 600px; margin: 0 auto; 
                         padding: 20px; color: #333;">
                <div style="border-bottom: 3px solid #1976d2; padding-bottom: 10px; margin-bottom: 20px;">
                    <h1 style="color: #1976d2; margin: 0;">ZISK</h1>
                </div>
                {content}
                <div style="border-top: 1px solid #e0e0e0; padding-top: 15px; margin-top: 30px; 
                            color: #999; font-size: 12px;">
                    <p>Tento email bol odoslaný automaticky systémom ZISK. Neodpovedajte naň.</p>
                    <p>© 2026 ZISK – Žiarský Informačný Systém pre Kluby</p>
                </div>
            </body>
            </html>
            """;
    }
}