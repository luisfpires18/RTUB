using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Logging;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Application.Services.Email;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

// Email notifications: the private sending helpers (single, personalized batch, BCC).
public partial class EmailNotificationService
{
    /// <summary>
    /// Sends a single email
    /// </summary>
    private async Task SendSingleEmailAsync(EmailConfiguration config, string recipientEmail, string subject, string body)
    {
        using var smtpClient = _smtpFactory.CreateClient(config);

        var mailMessage = new MailMessage
        {
            From = new MailAddress(config.SenderEmail!, config.SenderName),
            Subject = subject,
            Body = body,
            IsBodyHtml = true,
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8
        };
        mailMessage.To.Add(recipientEmail);

        await smtpClient.SendMailAsync(mailMessage);
    }

    /// <summary>
    /// Sends personalized emails to a batch of recipients with bounded concurrency and progress reporting.
    ///
    /// PERFORMANCE CONSIDERATIONS:
    /// - Bounded concurrency: Uses SemaphoreSlim to limit concurrent sends (MaxConcurrentSends=10)
    ///   This prevents overwhelming the SMTP server and ensures stable performance
    /// - SMTP client reuse: Creates a single SMTP client for the entire batch to avoid connection overhead
    ///   Note: SmtpClient is NOT thread-safe, so sends are synchronized with smtpSemaphore
    /// - Scoped rendering: Each email template rendering gets its own scope (handled by EmailTemplateService)
    ///   This prevents ObjectDisposedException in concurrent scenarios
    ///
    /// WHY CENTRALIZED IN THE SERVICE (not in UI):
    /// - Consistency: All callers benefit from the same performance optimizations
    /// - Testing: Easier to test and benchmark performance in isolation
    /// - Separation of concerns: UI handles presentation, service handles email logic
    /// - Reusability: Can be called from background jobs, APIs, or UI without code duplication
    /// </summary>
    private async Task<(bool success, int count, string? errorMessage)> SendPersonalizedBatchAsync(
        EmailConfiguration config,
        List<string> recipientEmails,
        Dictionary<string, (string nickname, string fullName)> recipientData,
        string subject,
        Func<string, string, Task<string>> bodyRenderer,
        object entityId,
        string emailType,
        IProgress<EmailSendProgress>? progress = null)
    {
        // BATCHING: Maximum concurrent email sends to balance speed and server load
        const int MaxConcurrentSends = 10;
        int successCount = 0;
        int totalCount = recipientEmails.Count;
        var successCountLock = new object();

        // Report initial progress
        progress?.Report(new EmailSendProgress
        {
            Total = totalCount,
            Sent = 0,
            LastRecipient = null
        });

        // BOUNDED CONCURRENCY: SemaphoreSlim limits concurrent operations to prevent resource exhaustion
        using var semaphore = new SemaphoreSlim(MaxConcurrentSends, MaxConcurrentSends);

        // SMTP REUSE: Create one SMTP client with extended timeout for batch operations
        // This significantly improves performance by reusing the same connection
        using var smtpClient = _smtpFactory.CreateClient(config, SmtpClientFactory.BatchEmailTimeout);

        // THREAD SAFETY: SMTP operations need to be synchronized as SmtpClient is not thread-safe
        using var smtpSemaphore = new SemaphoreSlim(1, 1);

        var tasks = recipientEmails.Select(async email =>
        {
            if (string.IsNullOrWhiteSpace(email))
                return;

            await semaphore.WaitAsync();
            try
            {
                var (nickname, fullName) = recipientData.TryGetValue(email, out var data) ? data : ("", "");

                // Render template - this will create its own scope internally in EmailTemplateService
                var body = await bodyRenderer(nickname, fullName);

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(config.SenderEmail!, config.SenderName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true,
                    BodyEncoding = Encoding.UTF8,
                    SubjectEncoding = Encoding.UTF8
                };
                mailMessage.To.Add(email);

                // SMTP send needs to be synchronized as SmtpClient is not thread-safe
                await smtpSemaphore.WaitAsync();
                try
                {
                    await smtpClient.SendMailAsync(mailMessage);
                }
                finally
                {
                    smtpSemaphore.Release();
                }

                // Update success count and report progress (thread-safe)
                int currentCount;
                lock (successCountLock)
                {
                    successCount++;
                    currentCount = successCount;
                }

                // Report progress after each successful send
                progress?.Report(new EmailSendProgress
                {
                    Total = totalCount,
                    Sent = currentCount,
                    LastRecipient = email
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send {EmailType} email to {Email}", emailType, email);
            }
            finally
            {
                semaphore.Release();
            }
        }).ToList();

        await Task.WhenAll(tasks);

        _logger.LogInformation("{EmailType} emails sent for {EntityId} to {SuccessCount}/{TotalCount} members",
            emailType, entityId, successCount, recipientEmails.Count);

        return (successCount > 0, successCount, successCount < recipientEmails.Count ? "Alguns emails falharam" : null);
    }

    /// <summary>
    /// Sends a single email with all recipients in BCC
    /// </summary>
    private async Task<(bool success, int count, string? errorMessage)> SendBccEmailAsync(
        EmailConfiguration config,
        List<string> recipientEmails,
        string subject,
        string body,
        object entityId,
        string emailType)
    {
        using var smtpClient = _smtpFactory.CreateClient(config, SmtpClientFactory.BatchEmailTimeout);

        var mailMessage = new MailMessage
        {
            From = new MailAddress(config.SenderEmail!, config.SenderName),
            Subject = subject,
            Body = body,
            IsBodyHtml = true,
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8
        };

        foreach (var email in recipientEmails)
        {
            if (!string.IsNullOrWhiteSpace(email))
            {
                mailMessage.Bcc.Add(email);
            }
        }

        mailMessage.To.Add(config.SenderEmail!);

        await smtpClient.SendMailAsync(mailMessage);

        _logger.LogInformation("{EmailType} email successfully sent for {EntityId} to {RecipientCount} members",
            emailType, entityId, recipientEmails.Count);

        return (true, recipientEmails.Count, null);
    }
}
