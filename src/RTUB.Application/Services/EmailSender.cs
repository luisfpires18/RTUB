using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace RTUB.Application.Services;

/// <summary>
/// Email sender implementation that uses SMTP for sending emails
/// Used for ASP.NET Identity operations like password reset
/// </summary>
public class EmailSender : IEmailSender
{
    private readonly ILogger<EmailSender> _logger;
    private readonly IConfiguration _configuration;

    public EmailSender(ILogger<EmailSender> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        try
        {
            // Get email settings from configuration
            var smtpServer = _configuration["EmailSettings:SmtpServer"];
            var smtpPortStr = _configuration["EmailSettings:SmtpPort"];
            var smtpPort = int.TryParse(smtpPortStr, out var port) ? port : 587;
            var smtpUsername = _configuration["EmailSettings:SmtpUsername"];
            var smtpPassword = _configuration["EmailSettings:SmtpPassword"];
            var senderEmail = _configuration["EmailSettings:SenderEmail"];
            var senderName = _configuration["EmailSettings:SenderName"];
            var enableSslStr = _configuration["EmailSettings:EnableSsl"];
            var enableSsl = enableSslStr != "false"; // Default to true

            if (string.IsNullOrEmpty(senderEmail))
            {
                _logger.LogError("SenderEmail is not configured in EmailSettings");
                return;
            }

            // Check if SMTP is configured
            if (string.IsNullOrEmpty(smtpServer) || string.IsNullOrEmpty(smtpPassword) || smtpPassword == "YOUR_APP_PASSWORD_HERE")
            {
                return;
            }

            // Send email via SMTP
            using var smtpClient = new SmtpClient(smtpServer, smtpPort)
            {
                Credentials = new NetworkCredential(smtpUsername, smtpPassword),
                EnableSsl = enableSsl
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(senderEmail, senderName),
                Subject = subject,
                Body = htmlMessage,
                IsBodyHtml = true,
                BodyEncoding = Encoding.UTF8,
                SubjectEncoding = Encoding.UTF8
            };
            mailMessage.To.Add(email);

            await smtpClient.SendMailAsync(mailMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email with subject: {Subject}", subject);
            // Don't fail the operation if email fails
        }
    }
}
