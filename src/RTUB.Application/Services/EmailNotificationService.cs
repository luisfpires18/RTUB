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
            // Get email settings from configuration
            var recipientEmail = _configuration["EmailSettings:RecipientEmail"];
            var smtpServer = _configuration["EmailSettings:SmtpServer"];
            var smtpPortStr = _configuration["EmailSettings:SmtpPort"];
            var smtpPort = int.TryParse(smtpPortStr, out var port) ? port : 587;
            var smtpUsername = _configuration["EmailSettings:SmtpUsername"];
            var smtpPassword = _configuration["EmailSettings:SmtpPassword"];
            var senderEmail = _configuration["EmailSettings:SenderEmail"];
            var senderName = _configuration["EmailSettings:SenderName"] ?? "RTUB 1991";
            var enableSslStr = _configuration["EmailSettings:EnableSsl"];
            var enableSsl = enableSslStr != "false"; // Default to true

            // Validate required email settings
            if (string.IsNullOrEmpty(recipientEmail))
            {
                _logger.LogError("RecipientEmail is not configured in EmailSettings");
                return;
            }

            if (string.IsNullOrEmpty(senderEmail))
            {
                _logger.LogError("SenderEmail is not configured in EmailSettings");
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
                From = new MailAddress(senderEmail, senderName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true,
                BodyEncoding = Encoding.UTF8,
                SubjectEncoding = Encoding.UTF8
            };

            mailMessage.To.Add(recipientEmail);

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

            var subject = "Bem-vindo à RTUB - Credenciais de Acesso";

            var body = await _templateRenderer.RenderWelcomeEmailAsync(
                normalizedUserName,
                fullName,
                nickname,
                password);

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
            // Get email settings from configuration
            var smtpServer = _configuration["EmailSettings:SmtpServer"];
            var smtpPortStr = _configuration["EmailSettings:SmtpPort"];
            var smtpPort = int.TryParse(smtpPortStr, out var port) ? port : 587;
            var smtpUsername = _configuration["EmailSettings:SmtpUsername"];
            var smtpPassword = _configuration["EmailSettings:SmtpPassword"];
            var senderEmail = _configuration["EmailSettings:SenderEmail"];
            var senderName = _configuration["EmailSettings:SenderName"] ?? "RTUB 1991";
            var enableSslStr = _configuration["EmailSettings:EnableSsl"];
            var enableSsl = enableSslStr != "false"; // Default to true

            // Validate required email settings
            if (string.IsNullOrEmpty(senderEmail))
            {
                _logger.LogError("SenderEmail is not configured in EmailSettings");
                return (false, 0, "Configuração de email não está completa.");
            }

            // Check if SMTP is configured
            if (string.IsNullOrEmpty(smtpServer) || string.IsNullOrEmpty(smtpPassword) || smtpPassword == "YOUR_APP_PASSWORD_HERE")
            {
                _logger.LogWarning("SMTP not configured, skipping event notification email");
                return (false, 0, "Servidor de email não configurado.");
            }

            // Validate recipients
            if (recipientEmails == null || !recipientEmails.Any())
            {
                _logger.LogWarning("No recipient emails provided for event notification");
                return (false, 0, "Nenhum destinatário encontrado.");
            }

            var subject = $"Nova atuação: {eventTitle} — {eventDate:dd MMM yyyy}";

            // Format date in PT-PT format
            var dateFormatted = eventDate.ToString("dddd, dd 'de' MMMM 'de' yyyy", 
                new System.Globalization.CultureInfo("pt-PT"));

            // If recipient data is provided, send personalized emails to each recipient
            if (recipientData != null && recipientData.Any())
            {
                int successCount = 0;
                using var smtpClient = new SmtpClient(smtpServer, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUsername, smtpPassword),
                    EnableSsl = enableSsl,
                    Timeout = 30000 // 30 second timeout
                };

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
                            From = new MailAddress(senderEmail, senderName),
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
                using var smtpClient = new SmtpClient(smtpServer, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUsername, smtpPassword),
                    EnableSsl = enableSsl,
                    Timeout = 30000 // 30 second timeout for batch emails
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(senderEmail, senderName),
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
                mailMessage.To.Add(senderEmail);

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

    // Helper methods for email body generation (to be implemented when actual email sending is added)
    // private string BuildStatusChangeEmailBody(string name, int requestId, RequestStatus oldStatus, RequestStatus newStatus) { ... }
    // private string BuildNewRequestEmailBody(string name, string email, string eventType, int requestId) { ... }

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
            // Get email settings from configuration
            var smtpServer = _configuration["EmailSettings:SmtpServer"];
            var smtpPortStr = _configuration["EmailSettings:SmtpPort"];
            var smtpPort = int.TryParse(smtpPortStr, out var port) ? port : 587;
            var smtpUsername = _configuration["EmailSettings:SmtpUsername"];
            var smtpPassword = _configuration["EmailSettings:SmtpPassword"];
            var senderEmail = _configuration["EmailSettings:SenderEmail"];
            var senderName = _configuration["EmailSettings:SenderName"] ?? "RTUB 1991";
            var enableSslStr = _configuration["EmailSettings:EnableSsl"];
            var enableSsl = enableSslStr != "false"; // Default to true

            // Validate required email settings
            if (string.IsNullOrEmpty(senderEmail))
            {
                _logger.LogError("SenderEmail is not configured in EmailSettings");
                return (false, 0, "Configuração de email não está completa.");
            }

            // Check if SMTP is configured
            if (string.IsNullOrEmpty(smtpServer) || string.IsNullOrEmpty(smtpPassword) || smtpPassword == "YOUR_APP_PASSWORD_HERE")
            {
                _logger.LogWarning("SMTP not configured, skipping birthday notification email");
                return (false, 0, "Servidor de email não configurado.");
            }

            // Validate recipients
            if (recipientEmails == null || !recipientEmails.Any())
            {
                _logger.LogWarning("No recipient emails provided for birthday notification");
                return (false, 0, "Nenhum destinatário encontrado.");
            }

            var subject = $"🎉 {birthdayPersonNickname} está de aniversário!";

            int successCount = 0;
            using var smtpClient = new SmtpClient(smtpServer, smtpPort)
            {
                Credentials = new NetworkCredential(smtpUsername, smtpPassword),
                EnableSsl = enableSsl,
                Timeout = 30000 // 30 second timeout
            };

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
                        From = new MailAddress(senderEmail, senderName),
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
            // Get email settings from configuration
            var smtpServer = _configuration["EmailSettings:SmtpServer"];
            var smtpPortStr = _configuration["EmailSettings:SmtpPort"];
            var smtpPort = int.TryParse(smtpPortStr, out var port) ? port : 587;
            var smtpUsername = _configuration["EmailSettings:SmtpUsername"];
            var smtpPassword = _configuration["EmailSettings:SmtpPassword"];
            var senderEmail = _configuration["EmailSettings:SenderEmail"];
            var senderName = _configuration["EmailSettings:SenderName"] ?? "RTUB 1991";
            var enableSslStr = _configuration["EmailSettings:EnableSsl"];
            var enableSsl = enableSslStr != "false"; // Default to true

            // Validate required email settings
            if (string.IsNullOrEmpty(senderEmail))
            {
                _logger.LogError("SenderEmail is not configured in EmailSettings");
                return (false, 0, "Configuração de email não está completa.");
            }

            // Check if SMTP is configured
            if (string.IsNullOrEmpty(smtpServer) || string.IsNullOrEmpty(smtpPassword) || smtpPassword == "YOUR_APP_PASSWORD_HERE")
            {
                _logger.LogWarning("SMTP not configured, skipping event cancellation notification email");
                return (false, 0, "Servidor de email não configurado.");
            }

            // Validate recipients
            if (recipientEmails == null || !recipientEmails.Any())
            {
                _logger.LogWarning("No recipient emails provided for event cancellation notification");
                return (false, 0, "Nenhum destinatário encontrado.");
            }

            var subject = $"⚠️ Atuação cancelada: {eventTitle}";

            // Format date in PT-PT format
            var dateFormatted = eventDate.ToString("dddd, dd 'de' MMMM 'de' yyyy", 
                new System.Globalization.CultureInfo("pt-PT"));

            // If recipient data is provided, send personalized emails to each recipient
            if (recipientData != null && recipientData.Any())
            {
                int successCount = 0;
                using var smtpClient = new SmtpClient(smtpServer, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUsername, smtpPassword),
                    EnableSsl = enableSsl,
                    Timeout = 30000 // 30 second timeout
                };

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
                            From = new MailAddress(senderEmail, senderName),
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
            // Get email settings from configuration
            var smtpServer = _configuration["EmailSettings:SmtpServer"];
            var smtpPortStr = _configuration["EmailSettings:SmtpPort"];
            var smtpPort = int.TryParse(smtpPortStr, out var port) ? port : 587;
            var smtpUsername = _configuration["EmailSettings:SmtpUsername"];
            var smtpPassword = _configuration["EmailSettings:SmtpPassword"];
            var senderEmail = _configuration["EmailSettings:SenderEmail"];
            var senderName = _configuration["EmailSettings:SenderName"] ?? "RTUB 1991";
            var enableSslStr = _configuration["EmailSettings:EnableSsl"];
            var enableSsl = enableSslStr != "false"; // Default to true

            // Validate required email settings
            if (string.IsNullOrEmpty(senderEmail))
            {
                _logger.LogError("SenderEmail is not configured in EmailSettings");
                return (false, 0, "Configuração de email não está completa.");
            }

            // Check if SMTP is configured
            if (string.IsNullOrEmpty(smtpServer) || string.IsNullOrEmpty(smtpPassword) || smtpPassword == "YOUR_APP_PASSWORD_HERE")
            {
                _logger.LogWarning("SMTP not configured, skipping event reminder notification email");
                return (false, 0, "Servidor de email não configurado.");
            }

            // Validate recipients
            if (recipientEmails == null || !recipientEmails.Any())
            {
                _logger.LogWarning("No recipient emails provided for event reminder notification");
                return (false, 0, "Nenhum destinatário encontrado.");
            }

            // Calculate days until event
            var daysUntilEvent = (int)Math.Ceiling((eventDate.Date - DateTime.UtcNow.Date).TotalDays);
            
            var subject = $"Lembrete: {eventTitle} — faltam {daysUntilEvent} {(daysUntilEvent == 1 ? "dia" : "dias")}";

            // Format date in PT-PT format
            var dateFormatted = eventDate.ToString("dddd, dd 'de' MMMM 'de' yyyy", 
                new System.Globalization.CultureInfo("pt-PT"));

            // If recipient data is provided, send personalized emails to each recipient
            if (recipientData != null && recipientData.Any())
            {
                int successCount = 0;
                using var smtpClient = new SmtpClient(smtpServer, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUsername, smtpPassword),
                    EnableSsl = enableSsl,
                    Timeout = 30000 // 30 second timeout
                };

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
                            From = new MailAddress(senderEmail, senderName),
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
}