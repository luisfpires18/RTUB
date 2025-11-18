using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RTUB.Application.Interfaces;
using RTUB.Core.Enums;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace RTUB.Application.Services;

/// <summary>
/// Service for sending email notifications with rate limiting and caching
/// </summary>
public class EmailNotificationService : IEmailNotificationService
{
    private readonly ILogger<EmailNotificationService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;
    private readonly IEmailTemplateRenderer _templateRenderer;

    // Configuration key constants
    private const string EmailSettingsPrefix = "EmailSettings:";
    private const string RecipientEmailKey = EmailSettingsPrefix + "RecipientEmail";
    private const string SmtpServerKey = EmailSettingsPrefix + "SmtpServer";
    private const string SmtpPortKey = EmailSettingsPrefix + "SmtpPort";
    private const string SmtpUsernameKey = EmailSettingsPrefix + "SmtpUsername";
    private const string SmtpPasswordKey = EmailSettingsPrefix + "SmtpPassword";
    private const string SenderEmailKey = EmailSettingsPrefix + "SenderEmail";
    private const string SenderNameKey = EmailSettingsPrefix + "SenderName";
    private const string EnableSslKey = EmailSettingsPrefix + "EnableSsl";
    private const string DefaultSenderName = "RTUB 1991";
    private const string PlaceholderPassword = "YOUR_APP_PASSWORD_HERE";
    private const int DefaultSmtpPort = 587;
    private const int DefaultSmtpTimeout = 10000;
    private const int BatchEmailTimeout = 30000;

    public EmailNotificationService(
        ILogger<EmailNotificationService> logger,
        IConfiguration configuration,
        IMemoryCache cache,
        IEmailTemplateRenderer templateRenderer)
    {
        _logger = logger;
        _configuration = configuration;
        _cache = cache;
        _templateRenderer = templateRenderer;
    }

    /// <summary>
    /// Gets email configuration from appsettings
    /// </summary>
    private EmailConfiguration GetEmailConfiguration()
    {
        var config = new EmailConfiguration
        {
            RecipientEmail = _configuration[RecipientEmailKey],
            SmtpServer = _configuration[SmtpServerKey],
            SmtpPort = int.TryParse(_configuration[SmtpPortKey], out var port) ? port : DefaultSmtpPort,
            SmtpUsername = _configuration[SmtpUsernameKey],
            SmtpPassword = _configuration[SmtpPasswordKey],
            SenderEmail = _configuration[SenderEmailKey],
            SenderName = _configuration[SenderNameKey] ?? DefaultSenderName,
            EnableSsl = _configuration[EnableSslKey] != "false" // Default to true
        };

        return config;
    }

    /// <summary>
    /// Creates and configures an SMTP client
    /// </summary>
    private SmtpClient CreateSmtpClient(EmailConfiguration config, int timeout = DefaultSmtpTimeout)
    {
        if (string.IsNullOrEmpty(config.SmtpUsername) || string.IsNullOrEmpty(config.SmtpPassword))
        {
            throw new InvalidOperationException(
                "SMTP credentials are not configured. Check EmailSettings:SmtpUsername and EmailSettings:SmtpPassword settings.");
        }

        return new SmtpClient(config.SmtpServer, config.SmtpPort)
        {
            Credentials = new NetworkCredential(config.SmtpUsername, config.SmtpPassword),
            EnableSsl = config.EnableSsl,
            Timeout = timeout
        };
    }

    /// <summary>
    /// Validates that SMTP is properly configured
    /// </summary>
    private bool IsSmtpConfigured(EmailConfiguration config)
    {
        return !string.IsNullOrEmpty(config.SmtpServer) 
            && !string.IsNullOrEmpty(config.SmtpUsername)
            && !string.IsNullOrEmpty(config.SmtpPassword) 
            && config.SmtpPassword != PlaceholderPassword;
    }

    /// <summary>
    /// Email configuration model
    /// </summary>
    private class EmailConfiguration
    {
        public string? RecipientEmail { get; set; }
        public string? SmtpServer { get; set; }
        public int SmtpPort { get; set; }
        public string? SmtpUsername { get; set; }
        public string? SmtpPassword { get; set; }
        public string? SenderEmail { get; set; }
        public string SenderName { get; set; } = DefaultSenderName;
        public bool EnableSsl { get; set; }
    }

    /// <summary>
    /// Check if we should rate-limit email sending (prevents duplicate emails)
    /// </summary>
    private bool ShouldRateLimitEmail(string cacheKey)
    {
        if (_cache.TryGetValue<bool>(cacheKey, out _))
        {
            return true; // Already sent recently
        }

        // Mark as sent for the next 5 minutes
        _cache.Set(cacheKey, true, TimeSpan.FromMinutes(5));
        return false;
    }

    /// <inheritdoc/>
    public async Task SendRequestStatusChangedAsync(int requestId, string requestName, string requestEmail, RequestStatus oldStatus, RequestStatus newStatus)
    {
        // Note: This method is deprecated and kept for backward compatibility only.
        // Email notifications for request status changes are not currently implemented.
        // Use the full overload SendNewRequestNotificationAsync for new request notifications.
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
        // Rate limit: Prevent duplicate emails for the same request within 5 minutes
        var rateLimitKey = $"email-request-{requestId}";
        if (ShouldRateLimitEmail(rateLimitKey))
        {
            return;
        }

        try
        {
            var config = GetEmailConfiguration();

            // Validate required email settings
            if (string.IsNullOrEmpty(config.RecipientEmail))
            {
                _logger.LogError("RecipientEmail is not configured in EmailSettings");
                return;
            }

            if (string.IsNullOrEmpty(config.SenderEmail))
            {
                _logger.LogError("SenderEmail is not configured in EmailSettings");
                return;
            }

            // Check if SMTP is configured
            if (!IsSmtpConfigured(config))
            {
                return;
            }

            var subject = $"Novo Pedido de Atuação - {requestName}";

            var dateInfo = preferredEndDate.HasValue
                ? $"De {preferredDate:dd/MM/yyyy} até {preferredEndDate:dd/MM/yyyy}"
                : preferredDate.ToString("dd/MM/yyyy");

            var body = await _templateRenderer.RenderNewRequestNotificationAsync(
                requestName,
                requestEmail,
                phone,
                eventType,
                dateInfo,
                location,
                message,
                createdAt);

            // Send email via SMTP
            using var smtpClient = CreateSmtpClient(config);

            var mailMessage = new MailMessage
            {
                From = new MailAddress(config.SenderEmail, config.SenderName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true,
                BodyEncoding = Encoding.UTF8,
                SubjectEncoding = Encoding.UTF8
            };

            mailMessage.To.Add(config.RecipientEmail);

            await smtpClient.SendMailAsync(mailMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email notification for request #{RequestId}", requestId);
            // Don't fail the request if email fails
        }
    }

    /// <inheritdoc/>
    public async Task SendWelcomeEmailAsync(string userName, string email, string fullName, string nickname, string password)
    {
        // Rate limit: Prevent duplicate welcome emails for the same user
        var normalizedUserName = userName?.ToLower() ?? "unknown";
        var rateLimitKey = $"email-welcome-{normalizedUserName}";
        if (ShouldRateLimitEmail(rateLimitKey))
        {
            return;
        }

        try
        {
            var config = GetEmailConfiguration();

            // Validate required email settings
            if (string.IsNullOrEmpty(email))
            {
                _logger.LogError("Recipient email is null or empty");
                return;
            }

            if (string.IsNullOrEmpty(config.SenderEmail))
            {
                _logger.LogError("SenderEmail is not configured in EmailSettings");
                return;
            }

            // Check if SMTP is configured
            if (!IsSmtpConfigured(config))
            {
                return;
            }

            var subject = "Bem-vindo à RTUB - Credenciais de Acesso";

            var body = await _templateRenderer.RenderWelcomeEmailAsync(
                normalizedUserName,
                fullName,
                nickname,
                password);

            // Send email via SMTP
            using var smtpClient = CreateSmtpClient(config);

            var mailMessage = new MailMessage
            {
                From = new MailAddress(config.SenderEmail, config.SenderName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true,
                BodyEncoding = Encoding.UTF8,
                SubjectEncoding = Encoding.UTF8
            };
            mailMessage.To.Add(email);

            await smtpClient.SendMailAsync(mailMessage);
            _logger.LogInformation("Welcome email successfully sent to new member: {UserName}", userName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending welcome email to new member");
            // Don't fail the member creation if email fails
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
        Dictionary<string, (string nickname, string fullName)>? recipientData = null)
    {
        // Rate limit: Prevent duplicate emails for the same event within 5 minutes
        var rateLimitKey = $"email-event-{eventId}";
        if (ShouldRateLimitEmail(rateLimitKey))
        {
            return (false, 0, "Email já enviado recentemente para este evento.");
        }

        try
        {
            var config = GetEmailConfiguration();

            // Validate recipients first to fail fast before checking SMTP configuration
            if (recipientEmails is null || !recipientEmails.Any())
            {
                _logger.LogWarning("No recipient emails provided for event notification");
                return (false, 0, "Nenhum destinatário encontrado.");
            }

            // Validate required email settings
            if (string.IsNullOrEmpty(config.SenderEmail))
            {
                _logger.LogError("SenderEmail is not configured in EmailSettings");
                return (false, 0, "Configuração de email não está completa.");
            }

            // Check if SMTP is configured
            if (!IsSmtpConfigured(config))
            {
                _logger.LogWarning("SMTP not configured, skipping event notification email");
                return (false, 0, "Servidor de email não configurado.");
            }

            var subject = $"Nova atuação: {eventTitle} — {eventDate:dd MMM yyyy}";

            // Format date in PT-PT format
            var dateFormatted = eventDate.ToString("dddd, dd 'de' MMMM 'de' yyyy", 
                new System.Globalization.CultureInfo("pt-PT"));

            // If recipient data is provided, send personalized emails to each recipient
            if (recipientData is not null && recipientData.Any())
            {
                int successCount = 0;
                using var smtpClient = CreateSmtpClient(config, BatchEmailTimeout);

                foreach (var email in recipientEmails)
                {
                    if (string.IsNullOrWhiteSpace(email))
                        continue;

                    try
                    {
                        // Get nickname and full name for this recipient
                        var (nickname, fullName) = recipientData.TryGetValue(email, out var data) 
                            ? data 
                            : ("", "");

                        // Render personalized email
                        var body = await _templateRenderer.RenderEventNotificationAsync(
                            eventTitle,
                            dateFormatted,
                            eventLocation,
                            eventLink,
                            nickname,
                            fullName);

                        var mailMessage = new MailMessage
                        {
                            From = new MailAddress(config.SenderEmail, config.SenderName),
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
                        _logger.LogWarning(ex, "Failed to send personalized email to {Email}", email);
                    }
                }

                _logger.LogInformation("Event notification emails sent for event {EventId} to {SuccessCount}/{TotalCount} members", 
                    eventId, successCount, recipientEmails.Count);

                return (successCount > 0, successCount, successCount < recipientEmails.Count ? "Alguns emails falharam" : null);
            }
            else
            {
                // No personalization - send one email with all recipients in BCC (original behavior)
                var body = await _templateRenderer.RenderEventNotificationAsync(
                    eventTitle,
                    dateFormatted,
                    eventLocation,
                    eventLink);

                // Send email via SMTP
                using var smtpClient = CreateSmtpClient(config, BatchEmailTimeout);

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(config.SenderEmail, config.SenderName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true,
                    BodyEncoding = Encoding.UTF8,
                    SubjectEncoding = Encoding.UTF8
                };

                // Add all recipients as BCC to hide recipient list
                foreach (var email in recipientEmails)
                {
                    if (!string.IsNullOrWhiteSpace(email))
                    {
                        mailMessage.Bcc.Add(email);
                    }
                }

                // Add sender as the To address (required by some SMTP servers)
                mailMessage.To.Add(config.SenderEmail);

                await smtpClient.SendMailAsync(mailMessage);
                
                _logger.LogInformation("Event notification email successfully sent for event {EventId} to {RecipientCount} members", 
                    eventId, recipientEmails.Count);

                return (true, recipientEmails.Count, null);
            }
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
        // Rate limit: Prevent duplicate emails for the same birthday within 5 minutes
        var rateLimitKey = $"email-birthday-{birthdayPersonId}-{DateTime.UtcNow:yyyy-MM-dd}";
        if (ShouldRateLimitEmail(rateLimitKey))
        {
            return (false, 0, "Email de aniversário já enviado recentemente.");
        }

        try
        {
            var config = GetEmailConfiguration();

            // Validate recipients first
            if (recipientEmails is null || !recipientEmails.Any())
            {
                _logger.LogWarning("No recipient emails provided for birthday notification");
                return (false, 0, "Nenhum destinatário encontrado.");
            }

            // Validate required email settings
            if (string.IsNullOrEmpty(config.SenderEmail))
            {
                _logger.LogError("SenderEmail is not configured in EmailSettings");
                return (false, 0, "Configuração de email não está completa.");
            }

            // Check if SMTP is configured
            if (!IsSmtpConfigured(config))
            {
                _logger.LogWarning("SMTP not configured, skipping birthday notification email");
                return (false, 0, "Servidor de email não configurado.");
            }

            var subject = $"🎉 {birthdayPersonNickname} está de aniversário!";

            int successCount = 0;
            using var smtpClient = CreateSmtpClient(config, BatchEmailTimeout);

            foreach (var email in recipientEmails)
            {
                if (string.IsNullOrWhiteSpace(email))
                    continue;

                try
                {
                    // Get nickname and full name for this recipient
                    var (nickname, fullName) = recipientData.TryGetValue(email, out var data) 
                        ? data 
                        : ("", "");

                    // Render personalized email
                    var body = await _templateRenderer.RenderBirthdayNotificationAsync(
                        birthdayPersonNickname,
                        birthdayPersonFullName,
                        nickname,
                        fullName);

                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(config.SenderEmail, config.SenderName),
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
                    _logger.LogWarning(ex, "Failed to send birthday notification email to {Email}", email);
                }
            }

            _logger.LogInformation("Birthday notification emails sent for user {UserId} to {SuccessCount}/{TotalCount} members", 
                birthdayPersonId, successCount, recipientEmails.Count);

            return (successCount > 0, successCount, successCount < recipientEmails.Count ? "Alguns emails falharam" : null);
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
        // Rate limit: Prevent duplicate emails for the same event cancellation within 5 minutes
        var rateLimitKey = $"email-event-cancellation-{eventId}";
        if (ShouldRateLimitEmail(rateLimitKey))
        {
            return (false, 0, "Email de cancelamento já enviado recentemente.");
        }

        try
        {
            var config = GetEmailConfiguration();

            // Validate recipients first
            if (recipientEmails is null || !recipientEmails.Any())
            {
                _logger.LogWarning("No recipient emails provided for event cancellation notification");
                return (false, 0, "Nenhum destinatário encontrado.");
            }

            // Validate required email settings
            if (string.IsNullOrEmpty(config.SenderEmail))
            {
                _logger.LogError("SenderEmail is not configured in EmailSettings");
                return (false, 0, "Configuração de email não está completa.");
            }

            // Check if SMTP is configured
            if (!IsSmtpConfigured(config))
            {
                _logger.LogWarning("SMTP not configured, skipping event cancellation notification email");
                return (false, 0, "Servidor de email não configurado.");
            }

            var subject = $"⚠️ Atuação cancelada: {eventTitle}";

            // Format date in PT-PT format
            var dateFormatted = eventDate.ToString("dddd, dd 'de' MMMM 'de' yyyy", 
                new System.Globalization.CultureInfo("pt-PT"));

            // If recipient data is provided, send personalized emails to each recipient
            if (recipientData is not null && recipientData.Any())
            {
                int successCount = 0;
                using var smtpClient = CreateSmtpClient(config, BatchEmailTimeout);

                foreach (var email in recipientEmails)
                {
                    if (string.IsNullOrWhiteSpace(email))
                        continue;

                    try
                    {
                        // Get nickname and full name for this recipient
                        var (nickname, fullName) = recipientData.TryGetValue(email, out var data) 
                            ? data 
                            : ("", "");

                        // Render personalized email
                        var body = await _templateRenderer.RenderEventCancellationNotificationAsync(
                            eventTitle,
                            dateFormatted,
                            eventLocation,
                            cancellationReason,
                            eventLink,
                            nickname,
                            fullName);

                        var mailMessage = new MailMessage
                        {
                            From = new MailAddress(config.SenderEmail, config.SenderName),
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
                        _logger.LogWarning(ex, "Failed to send event cancellation notification email to {Email}", email);
                    }
                }

                _logger.LogInformation("Event cancellation notification emails sent for event {EventId} to {SuccessCount}/{TotalCount} members", 
                    eventId, successCount, recipientEmails.Count);

                return (successCount > 0, successCount, successCount < recipientEmails.Count ? "Alguns emails falharam" : null);
            }

            // If no recipient data, send generic emails (fallback - not personalized)
            _logger.LogWarning("No recipient data provided for event cancellation notification, emails will not be personalized");
            return (false, 0, "Dados de destinatários não fornecidos.");
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
        string eventDescription = "")
    {
        // Rate limit: Prevent duplicate emails for the same event reminder within 5 minutes
        var rateLimitKey = $"email-event-reminder-{eventId}";
        if (ShouldRateLimitEmail(rateLimitKey))
        {
            return (false, 0, "Email de lembrete já enviado recentemente.");
        }

        try
        {
            var config = GetEmailConfiguration();

            // Validate recipients first
            if (recipientEmails is null || !recipientEmails.Any())
            {
                _logger.LogWarning("No recipient emails provided for event reminder notification");
                return (false, 0, "Nenhum destinatário encontrado.");
            }

            // Validate required email settings
            if (string.IsNullOrEmpty(config.SenderEmail))
            {
                _logger.LogError("SenderEmail is not configured in EmailSettings");
                return (false, 0, "Configuração de email não está completa.");
            }

            // Check if SMTP is configured
            if (!IsSmtpConfigured(config))
            {
                _logger.LogWarning("SMTP not configured, skipping event reminder notification email");
                return (false, 0, "Servidor de email não configurado.");
            }

            // Calculate days until event
            var daysUntilEvent = (int)Math.Ceiling((eventDate.Date - DateTime.UtcNow.Date).TotalDays);
            
            var subject = $"Lembrete: {eventTitle} — faltam {daysUntilEvent} {(daysUntilEvent == 1 ? "dia" : "dias")}";

            // Format date in PT-PT format
            var dateFormatted = eventDate.ToString("dddd, dd 'de' MMMM 'de' yyyy", 
                new System.Globalization.CultureInfo("pt-PT"));

            // If recipient data is provided, send personalized emails to each recipient
            if (recipientData is not null && recipientData.Any())
            {
                int successCount = 0;
                using var smtpClient = CreateSmtpClient(config, BatchEmailTimeout);

                foreach (var email in recipientEmails)
                {
                    if (string.IsNullOrWhiteSpace(email))
                        continue;

                    try
                    {
                        // Get nickname and full name for this recipient
                        var (nickname, fullName) = recipientData.TryGetValue(email, out var data) 
                            ? data 
                            : ("", "");

                        // Render personalized email
                        var body = await _templateRenderer.RenderEventReminderNotificationAsync(
                            eventTitle,
                            dateFormatted,
                            eventLocation,
                            eventLink,
                            daysUntilEvent,
                            nickname,
                            fullName,
                            eventDescription);

                        var mailMessage = new MailMessage
                        {
                            From = new MailAddress(config.SenderEmail, config.SenderName),
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
                        _logger.LogWarning(ex, "Failed to send event reminder notification email to {Email}", email);
                    }
                }

                _logger.LogInformation("Event reminder notification emails sent for event {EventId} to {SuccessCount}/{TotalCount} members", 
                    eventId, successCount, recipientEmails.Count);

                return (successCount > 0, successCount, successCount < recipientEmails.Count ? "Alguns emails falharam" : null);
            }

            // If no recipient data, send generic emails (fallback - not personalized)
            _logger.LogWarning("No recipient data provided for event reminder notification, emails will not be personalized");
            return (false, 0, "Dados de destinatários não fornecidos.");
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
        // Rate limit: Prevent duplicate emails for the same announcement within 5 minutes
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmm");
        var rateLimitKey = $"email-announcement-{timestamp}";
        if (ShouldRateLimitEmail(rateLimitKey))
        {
            return (false, 0, "Email de anúncio já enviado recentemente.");
        }

        try
        {
            var config = GetEmailConfiguration();

            // Validate recipients first
            if (recipientEmails is null || !recipientEmails.Any())
            {
                _logger.LogWarning("No recipient emails provided for announcement");
                return (false, 0, "Nenhum destinatário encontrado.");
            }

            // Validate required email settings
            if (string.IsNullOrEmpty(config.SenderEmail))
            {
                _logger.LogError("SenderEmail is not configured in EmailSettings");
                return (false, 0, "Configuração de email não está completa.");
            }

            // Check if SMTP is configured
            if (!IsSmtpConfigured(config))
            {
                _logger.LogWarning("SMTP not configured, skipping announcement email");
                return (false, 0, "Servidor de email não configurado.");
            }

            var subject = $"[RTUB] {title}";

            int successCount = 0;
            using var smtpClient = CreateSmtpClient(config, BatchEmailTimeout);

            foreach (var email in recipientEmails)
            {
                if (string.IsNullOrWhiteSpace(email))
                    continue;

                try
                {
                    // Get nickname and full name for this recipient
                    var (nickname, fullName) = recipientData.TryGetValue(email, out var data) 
                        ? data 
                        : ("", "");

                    // Render personalized email
                    var body = await _templateRenderer.RenderAnnouncementEmailAsync(
                        title,
                        content,
                        nickname,
                        fullName);

                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(config.SenderEmail, config.SenderName),
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
                    _logger.LogWarning(ex, "Failed to send announcement email to {Email}", email);
                }
            }

            _logger.LogInformation("Announcement emails sent to {SuccessCount}/{TotalCount} members", 
                successCount, recipientEmails.Count);

            return (successCount > 0, successCount, successCount < recipientEmails.Count ? "Alguns emails falharam" : null);
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
        // Rate limit: Prevent duplicate emails for the same meeting within 5 minutes
        var rateLimitKey = $"email-meeting-{meetingId}-{DateTime.UtcNow:yyyyMMddHHmm}";
        if (ShouldRateLimitEmail(rateLimitKey))
        {
            _logger.LogWarning("Rate limit hit for meeting notification {MeetingId}", meetingId);
            return (false, 0, "Email já enviado recentemente.");
        }

        try
        {
            var config = GetEmailConfiguration();

            // Validate email settings
            if (string.IsNullOrEmpty(config.SenderEmail) || string.IsNullOrEmpty(config.SmtpServer) || 
                string.IsNullOrEmpty(config.SmtpUsername) || string.IsNullOrEmpty(config.SmtpPassword))
            {
                _logger.LogError("Email settings are not properly configured");
                return (false, 0, "Configurações de email não definidas.");
            }

            if (recipientEmails is null || !recipientEmails.Any())
            {
                _logger.LogWarning("No recipients for meeting notification {MeetingId}", meetingId);
                return (false, 0, "Nenhum destinatário especificado.");
            }

            // Send emails to each recipient
            int successCount = 0;
            int failureCount = 0;

            using var smtpClient = CreateSmtpClient(config);

            foreach (var recipientEmail in recipientEmails)
            {
                try
                {
                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(config.SenderEmail, config.SenderName),
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
                    failureCount++;
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
        // Rate limit: Prevent duplicate username change emails for the same user
        var normalizedEmail = email?.ToLower() ?? "unknown";
        var rateLimitKey = $"email-username-changed-{normalizedEmail}";
        if (ShouldRateLimitEmail(rateLimitKey))
        {
            return;
        }

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

            // Validate required email settings
            if (string.IsNullOrEmpty(email))
            {
                _logger.LogError("Recipient email is null or empty");
                return;
            }

            if (string.IsNullOrEmpty(senderEmail))
            {
                _logger.LogError("SenderEmail is not configured in EmailSettings");
                return;
            }

            var subject = "Alcunha Definida - Novo Nome de Utilizador";

            var body = await _templateRenderer.RenderUsernameChangedEmailAsync(
                fullName,
                nickname,
                oldUsername,
                newUsername);

            // Check if SMTP is configured
            if (string.IsNullOrEmpty(smtpServer) || string.IsNullOrEmpty(smtpPassword) || smtpPassword == "YOUR_APP_PASSWORD_HERE")
            {
                return;
            }

            // Send email via SMTP
            using var smtpClient = new SmtpClient(smtpServer, smtpPort)
            {
                Credentials = new NetworkCredential(smtpUsername, smtpPassword),
                EnableSsl = enableSsl,
                Timeout = 10000 // 10 second timeout to prevent hanging
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(senderEmail, senderName ?? "RTUB"),
                Subject = subject,
                Body = body,
                IsBodyHtml = true,
                BodyEncoding = Encoding.UTF8,
                SubjectEncoding = Encoding.UTF8
            };
            mailMessage.To.Add(email);

            await smtpClient.SendMailAsync(mailMessage);
            _logger.LogInformation("Username changed email successfully sent to: {Email}", email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending username changed email to {Email}", email);
            // Don't fail the operation if email fails
        }
    }
}