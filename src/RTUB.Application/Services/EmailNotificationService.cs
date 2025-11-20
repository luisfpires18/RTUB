using Microsoft.Extensions.Logging;
using RTUB.Application.Interfaces;
using RTUB.Application.Services.Email;
using RTUB.Core.Enums;
using System.Net.Mail;
using System.Text;

namespace RTUB.Application.Services;

/// <summary>
/// Service for sending email notifications
/// Refactored to follow Single Responsibility Principle
/// </summary>
public class EmailNotificationService : IEmailNotificationService
{
    private readonly ILogger<EmailNotificationService> _logger;
    private readonly EmailConfigurationProvider _configProvider;
    private readonly SmtpClientFactory _smtpFactory;
    private readonly EmailRateLimiter _rateLimiter;
    private readonly IEmailTemplateRenderer _templateRenderer;

    public EmailNotificationService(
        ILogger<EmailNotificationService> logger,
        EmailConfigurationProvider configProvider,
        SmtpClientFactory smtpFactory,
        EmailRateLimiter rateLimiter,
        IEmailTemplateRenderer templateRenderer)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
        _smtpFactory = smtpFactory ?? throw new ArgumentNullException(nameof(smtpFactory));
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
        _templateRenderer = templateRenderer ?? throw new ArgumentNullException(nameof(templateRenderer));
    }

    /// <inheritdoc/>
    public async Task SendRequestStatusChangedAsync(int requestId, string requestName, string requestEmail, 
        RequestStatus oldStatus, RequestStatus newStatus)
    {
        // Note: This method is deprecated and kept for backward compatibility only.
        await Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task SendNewRequestNotificationAsync(int requestId, string requestName, string requestEmail, string eventType)
    {
        // Simple overload - kept for backward compatibility
        await Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task SendNewRequestNotificationAsync(int requestId, string requestName, string requestEmail, string phone,
        string eventType, DateTime preferredDate, DateTime? preferredEndDate, string location, string message, DateTime createdAt)
    {
        var rateLimitKey = $"email-request-{requestId}";
        if (_rateLimiter.ShouldRateLimit(rateLimitKey))
        {
            return;
        }

        try
        {
            var config = _configProvider.GetConfiguration();

            if (!_configProvider.ValidateRecipientEmail(config) || !_configProvider.ValidateSenderEmail(config))
            {
                return;
            }

            if (!_configProvider.IsSmtpConfigured(config))
            {
                return;
            }

            var subject = $"Novo Pedido de Atuação - {requestName}";

            var dateInfo = preferredEndDate.HasValue
                ? $"De {preferredDate:dd/MM/yyyy} até {preferredEndDate:dd/MM/yyyy}"
                : preferredDate.ToString("dd/MM/yyyy");

            var body = await _templateRenderer.RenderNewRequestNotificationAsync(
                requestName, requestEmail, phone, eventType, dateInfo, location, message, createdAt);

            await SendSingleEmailAsync(config, config.RecipientEmail!, subject, body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email notification for request #{RequestId}", requestId);
        }
    }

    /// <inheritdoc/>
    public async Task SendWelcomeEmailAsync(string userName, string email, string fullName, string nickname, string password)
    {
        var normalizedUserName = userName?.ToLower() ?? "unknown";
        var rateLimitKey = $"email-welcome-{normalizedUserName}";
        if (_rateLimiter.ShouldRateLimit(rateLimitKey))
        {
            return;
        }

        try
        {
            var config = _configProvider.GetConfiguration();

            if (string.IsNullOrEmpty(email))
            {
                _logger.LogError("Recipient email is null or empty");
                return;
            }

            if (!_configProvider.ValidateSenderEmail(config) || !_configProvider.IsSmtpConfigured(config))
            {
                return;
            }

            var subject = "Bem-vindo à RTUB - Credenciais de Acesso";
            var body = await _templateRenderer.RenderWelcomeEmailAsync(normalizedUserName, fullName, nickname, password);

            await SendSingleEmailAsync(config, email, subject, body);
            _logger.LogInformation("Welcome email successfully sent to new member: {UserName}", userName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending welcome email to new member");
        }
    }

    /// <inheritdoc/>
    public async Task<(bool success, int count, string? errorMessage)> SendEventNotificationAsync(
        int eventId,
        string eventTitle,
        DateTime eventDate,
        string eventLocation,
        string eventLink,
        List<string> recipientEmails,
        Dictionary<string, (string nickname, string fullName)>? recipientData = null,
        string eventDescription = "",
        DateTime? endDate = null)
    {
        var rateLimitKey = $"email-event-{eventId}";
        if (_rateLimiter.ShouldRateLimit(rateLimitKey))
        {
            return (false, 0, "Email já enviado recentemente para este evento.");
        }

        try
        {
            var config = _configProvider.GetConfiguration();

            if (recipientEmails is null || !recipientEmails.Any())
            {
                _logger.LogWarning("No recipient emails provided for event notification");
                return (false, 0, "Nenhum destinatário encontrado.");
            }

            if (!_configProvider.ValidateSenderEmail(config) || !_configProvider.IsSmtpConfigured(config))
            {
                return (false, 0, "Configuração de email não está completa.");
            }

            var subject = $"Nova atuação: {eventTitle} — {eventDate:dd MMM yyyy}";
            var dateFormatted = eventDate.ToString("dddd, dd 'de' MMMM 'de' yyyy",
                new System.Globalization.CultureInfo("pt-PT"));

            // Personalized emails
            if (recipientData is not null && recipientData.Any())
            {
                return await SendPersonalizedBatchAsync(config, recipientEmails, recipientData, subject,
                    async (nickname, fullName) => await _templateRenderer.RenderEventNotificationAsync(
                        eventTitle, dateFormatted, eventLocation, eventLink, nickname, fullName, eventDescription, endDate),
                    eventId, "event notification");
            }

            // Non-personalized (BCC mode)
            var body = await _templateRenderer.RenderEventNotificationAsync(eventTitle, dateFormatted, eventLocation, eventLink, "", "", eventDescription, endDate);
            return await SendBccEmailAsync(config, recipientEmails, subject, body, eventId, "event notification");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending event notification email for event {EventId}", eventId);
            return (false, 0, $"Erro ao enviar email: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<(bool success, int count, string? errorMessage)> SendBirthdayNotificationAsync(
        string birthdayPersonId,
        string birthdayPersonNickname,
        string birthdayPersonFullName,
        List<string> recipientEmails,
        Dictionary<string, (string nickname, string fullName)> recipientData)
    {
        var rateLimitKey = $"email-birthday-{birthdayPersonId}-{DateTime.UtcNow:yyyy-MM-dd}";
        if (_rateLimiter.ShouldRateLimit(rateLimitKey))
        {
            return (false, 0, "Email de aniversário já enviado recentemente.");
        }

        try
        {
            var config = _configProvider.GetConfiguration();

            if (recipientEmails is null || !recipientEmails.Any())
            {
                _logger.LogWarning("No recipient emails provided for birthday notification");
                return (false, 0, "Nenhum destinatário encontrado.");
            }

            if (!_configProvider.ValidateSenderEmail(config) || !_configProvider.IsSmtpConfigured(config))
            {
                return (false, 0, "Configuração de email não está completa.");
            }

            var subject = $"🎉 {birthdayPersonNickname} está de aniversário!";

            return await SendPersonalizedBatchAsync(config, recipientEmails, recipientData, subject,
                async (nickname, fullName) => await _templateRenderer.RenderBirthdayNotificationAsync(
                    birthdayPersonNickname, birthdayPersonFullName, nickname, fullName),
                birthdayPersonId, "birthday notification");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending birthday notification email for user {UserId}", birthdayPersonId);
            return (false, 0, $"Erro ao enviar email: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<(bool success, int count, string? errorMessage)> SendEventCancellationNotificationAsync(
        int eventId,
        string eventTitle,
        DateTime eventDate,
        string eventLocation,
        string cancellationReason,
        string eventLink,
        List<string> recipientEmails,
        Dictionary<string, (string nickname, string fullName)>? recipientData = null)
    {
        var rateLimitKey = $"email-event-cancellation-{eventId}";
        if (_rateLimiter.ShouldRateLimit(rateLimitKey))
        {
            return (false, 0, "Email de cancelamento já enviado recentemente.");
        }

        try
        {
            var config = _configProvider.GetConfiguration();

            if (recipientEmails is null || !recipientEmails.Any())
            {
                _logger.LogWarning("No recipient emails provided for event cancellation notification");
                return (false, 0, "Nenhum destinatário encontrado.");
            }

            if (!_configProvider.ValidateSenderEmail(config) || !_configProvider.IsSmtpConfigured(config))
            {
                return (false, 0, "Configuração de email não está completa.");
            }

            if (recipientData is null || !recipientData.Any())
            {
                _logger.LogWarning("No recipient data provided for event cancellation notification");
                return (false, 0, "Dados de destinatários não fornecidos.");
            }

            var subject = $"⚠️ Atuação cancelada: {eventTitle}";
            var dateFormatted = eventDate.ToString("dddd, dd 'de' MMMM 'de' yyyy",
                new System.Globalization.CultureInfo("pt-PT"));

            return await SendPersonalizedBatchAsync(config, recipientEmails, recipientData, subject,
                async (nickname, fullName) => await _templateRenderer.RenderEventCancellationNotificationAsync(
                    eventTitle, dateFormatted, eventLocation, cancellationReason, eventLink, nickname, fullName),
                eventId, "event cancellation notification");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending event cancellation notification email for event {EventId}", eventId);
            return (false, 0, $"Erro ao enviar email: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<(bool success, int count, string? errorMessage)> SendEventReminderNotificationAsync(
        int eventId,
        string eventTitle,
        DateTime eventDate,
        string eventLocation,
        string eventLink,
        List<string> recipientEmails,
        Dictionary<string, (string nickname, string fullName)>? recipientData = null,
        string eventDescription = "",
        DateTime? endDate = null)
    {
        var rateLimitKey = $"email-event-reminder-{eventId}";
        if (_rateLimiter.ShouldRateLimit(rateLimitKey))
        {
            return (false, 0, "Email de lembrete já enviado recentemente.");
        }

        try
        {
            var config = _configProvider.GetConfiguration();

            if (recipientEmails is null || !recipientEmails.Any())
            {
                _logger.LogWarning("No recipient emails provided for event reminder notification");
                return (false, 0, "Nenhum destinatário encontrado.");
            }

            if (!_configProvider.ValidateSenderEmail(config) || !_configProvider.IsSmtpConfigured(config))
            {
                return (false, 0, "Configuração de email não está completa.");
            }

            if (recipientData is null || !recipientData.Any())
            {
                _logger.LogWarning("No recipient data provided for event reminder notification");
                return (false, 0, "Dados de destinatários não fornecidos.");
            }

            var daysUntilEvent = (int)Math.Ceiling((eventDate.Date - DateTime.UtcNow.Date).TotalDays);
            var subject = $"Lembrete: {eventTitle} — faltam {daysUntilEvent} {(daysUntilEvent == 1 ? "dia" : "dias")}";
            var dateFormatted = eventDate.ToString("dddd, dd 'de' MMMM 'de' yyyy",
                new System.Globalization.CultureInfo("pt-PT"));

            return await SendPersonalizedBatchAsync(config, recipientEmails, recipientData, subject,
                async (nickname, fullName) => await _templateRenderer.RenderEventReminderNotificationAsync(
                    eventTitle, dateFormatted, eventLocation, eventLink, daysUntilEvent, nickname, fullName, eventDescription, endDate),
                eventId, "event reminder notification");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending event reminder notification email for event {EventId}", eventId);
            return (false, 0, $"Erro ao enviar email: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<(bool success, int count, string? errorMessage)> SendAnnouncementEmailAsync(
        string title,
        string content,
        List<string> recipientEmails,
        Dictionary<string, (string nickname, string fullName)> recipientData)
    {
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
                title, "announcement");
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
        Dictionary<string, (string nickname, string fullName)>? recipientData = null)
    {
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

    /// <inheritdoc/>
    public async Task SendUsernameChangedEmailAsync(string email, string fullName, string nickname, string oldUsername, string newUsername)
    {
        var normalizedEmail = email?.ToLower() ?? "unknown";
        var rateLimitKey = $"email-username-changed-{normalizedEmail}";
        if (_rateLimiter.ShouldRateLimit(rateLimitKey))
        {
            return;
        }

        try
        {
            var config = _configProvider.GetConfiguration();

            if (string.IsNullOrEmpty(email))
            {
                _logger.LogError("Recipient email is null or empty");
                return;
            }

            if (!_configProvider.ValidateSenderEmail(config) || !_configProvider.IsSmtpConfigured(config))
            {
                return;
            }

            var subject = "Alcunha Definida - Novo Nome de Utilizador";
            var body = await _templateRenderer.RenderUsernameChangedEmailAsync(fullName, nickname, oldUsername, newUsername);

            await SendSingleEmailAsync(config, email, subject, body);
            _logger.LogInformation("Username changed email successfully sent to: {Email}", email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending username changed email to {Email}", email);
        }
    }

    #region Private Helper Methods

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
    /// Sends personalized emails to a batch of recipients
    /// </summary>
    private async Task<(bool success, int count, string? errorMessage)> SendPersonalizedBatchAsync(
        EmailConfiguration config,
        List<string> recipientEmails,
        Dictionary<string, (string nickname, string fullName)> recipientData,
        string subject,
        Func<string, string, Task<string>> bodyRenderer,
        object entityId,
        string emailType)
    {
        int successCount = 0;
        using var smtpClient = _smtpFactory.CreateClient(config, SmtpClientFactory.BatchEmailTimeout);

        foreach (var email in recipientEmails)
        {
            if (string.IsNullOrWhiteSpace(email))
                continue;

            try
            {
                var (nickname, fullName) = recipientData.TryGetValue(email, out var data) ? data : ("", "");
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

                await smtpClient.SendMailAsync(mailMessage);
                successCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send {EmailType} email to {Email}", emailType, email);
            }
        }

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

    #endregion
}
