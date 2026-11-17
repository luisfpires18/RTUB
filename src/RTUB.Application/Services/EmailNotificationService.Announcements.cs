using System.Net.Mail;
using Microsoft.Extensions.Logging;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Application.Services.Email;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

// Email notifications: announcements and meetings.
public partial class EmailNotificationService
{
    /// <inheritdoc/>
    public async Task<(bool success, int count, string? errorMessage)> SendAnnouncementEmailAsync(
        string title,
        string content,
        List<string> recipientEmails,
        Dictionary<string, (string nickname, string fullName)> recipientData,
        IProgress<EmailSendProgress>? progress = null)
    {
        // Rate limiting: Uses timestamp to prevent accidental double-sends of announcements
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmm");
        var rateLimitKey = $"email-announcement-{timestamp}";
        if (_rateLimiter.ShouldRateLimit(rateLimitKey))
        {
            return (false, 0, "Email de anúncio já enviado recentemente.");
        }

        try
        {
            var config = _configProvider.GetConfiguration();

            if (recipientEmails is null || !recipientEmails.Any())
            {
                _logger.LogWarning("No recipient emails provided for announcement");
                return (false, 0, "Nenhum destinatário encontrado.");
            }

            if (!_configProvider.ValidateSenderEmail(config) || !_configProvider.IsSmtpConfigured(config))
            {
                return (false, 0, "Configuração de email não está completa.");
            }

            var subject = $"[RTUB] {title}";

            return await SendPersonalizedBatchAsync(config, recipientEmails, recipientData, subject,
                async (nickname, fullName) => await _templateRenderer.RenderAnnouncementEmailAsync(
                    title, content, nickname, fullName),
                title, "announcement", progress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending announcement email");
            return (false, 0, $"Erro ao enviar email: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<(bool success, int count, string? errorMessage)> SendMeetingNotificationAsync(
        int meetingId,
        string subject,
        string body,
        List<string> recipientEmails,
        Dictionary<string, (string nickname, string fullName)>? recipientData = null,
        IProgress<EmailSendProgress>? progress = null)
    {
        // Rate limiting: Prevents sending duplicate meeting notifications within same minute
        var rateLimitKey = $"email-meeting-{meetingId}-{DateTime.UtcNow:yyyyMMddHHmm}";
        if (_rateLimiter.ShouldRateLimit(rateLimitKey))
        {
            _logger.LogWarning("Rate limit hit for meeting notification {MeetingId}", meetingId);
            return (false, 0, "Email já enviado recentemente.");
        }

        try
        {
            var config = _configProvider.GetConfiguration();

            if (!_configProvider.ValidateSenderEmail(config) || !_configProvider.IsSmtpConfigured(config))
            {
                return (false, 0, "Configurações de email não definidas.");
            }

            if (recipientEmails is null || !recipientEmails.Any())
            {
                _logger.LogWarning("No recipients for meeting notification {MeetingId}", meetingId);
                return (false, 0, "Nenhum destinatário especificado.");
            }

            int successCount = 0;
            int totalCount = recipientEmails.Count;

            // Report initial progress
            progress?.Report(new EmailSendProgress
            {
                Total = totalCount,
                Sent = 0,
                LastRecipient = null
            });

            // Performance consideration: Reuse SMTP client for all sends
            // This avoids the overhead of creating a new connection for each email
            using var smtpClient = _smtpFactory.CreateClient(config);

            foreach (var recipientEmail in recipientEmails)
            {
                try
                {
                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(config.SenderEmail!, config.SenderName),
                        Subject = subject,
                        Body = body,
                        IsBodyHtml = true
                    };
                    mailMessage.To.Add(recipientEmail);

                    await smtpClient.SendMailAsync(mailMessage);
                    successCount++;
                    _logger.LogInformation("Meeting notification email sent to {Email} for meeting {MeetingId}",
                        recipientEmail, meetingId);

                    // Report progress after each successful send
                    progress?.Report(new EmailSendProgress
                    {
                        Total = totalCount,
                        Sent = successCount,
                        LastRecipient = recipientEmail
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send meeting notification email to {Email} for meeting {MeetingId}",
                        recipientEmail, meetingId);
                }
            }

            _logger.LogInformation("Meeting notification sent for meeting {MeetingId}: {SuccessCount}/{TotalCount} successful",
                meetingId, successCount, recipientEmails.Count);

            return (successCount > 0, successCount, successCount < recipientEmails.Count ? "Alguns emails falharam" : null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending meeting notification email for meeting {MeetingId}", meetingId);
            return (false, 0, $"Erro ao enviar email: {ex.Message}");
        }
    }
}
