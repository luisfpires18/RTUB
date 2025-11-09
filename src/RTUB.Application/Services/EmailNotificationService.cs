using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RTUB.Application.Interfaces;
using RTUB.Core.Enums;
using System.Net;
using System.Net.Mail;

namespace RTUB.Application.Services;

/// <summary>
/// Service for sending email notifications with rate limiting and caching
/// </summary>
public class EmailNotificationService : IEmailNotificationService
{
    private readonly ILogger<EmailNotificationService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;

    public EmailNotificationService(
        ILogger<EmailNotificationService> logger,
        IConfiguration configuration,
        IMemoryCache cache)
    {
        _logger = logger;
        _configuration = configuration;
        _cache = cache;
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
        // TODO: Implement actual email sending logic
        // Placeholder for actual implementation:
        // var subject = $"Request Status Update - #{requestId}";
        // var body = BuildStatusChangeEmailBody(requestName, requestId, oldStatus, newStatus);
        // await _emailSender.SendEmailAsync(requestEmail, subject, body);

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

            var body = $@"
Novo Pedido de Atuação Recebido

Nome: {requestName}
Email: {requestEmail}
Telefone: {phone}
Tipo de Evento: {eventType}
Data: {dateInfo}
Localização: {location}

Informações Adicionais:
{message}

Submetido em: {createdAt:dd/MM/yyyy HH:mm}
";

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
                IsBodyHtml = false
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
    public async Task SendWelcomeEmailAsync(string userName, string email, string firstName, string password)
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

            var body = $@"
Olá {firstName},

Bem-vindo à RTUB!

A sua conta foi criada com sucesso. Aqui estão as suas credenciais de acesso:

Nome de Utilizador: {normalizedUserName}
Palavra-passe: {password}

Por favor, aceda ao sistema em https://rtub.azurewebsites.net/ e altere a sua palavra-passe no seu perfil assim que possível.

Para alterar a palavra-passe:
1. Faça login com as credenciais acima
2. Vá para o seu Perfil
3. Clique em ""Alterar Palavra-passe""
4. Introduza a palavra-passe atual e escolha uma nova

Cumprimentos,
RTUB
";

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
                IsBodyHtml = false
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
        List<string> recipientEmails)
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
            var dateFormatted = eventDate.ToString("dddd, dd 'de' MMMM 'de' yyyy • HH:mm", 
                new System.Globalization.CultureInfo("pt-PT"));

            var body = $@"
Olá!

Há uma nova atuação agendada: {eventTitle}
📅 {dateFormatted}
📍 {eventLocation}

Consulta os detalhes no site e confirma a tua presença se fores.
{eventLink}

Obrigado,
RTUB
";

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
                IsBodyHtml = false
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending event notification email for event {EventId}", eventId);
            return (false, 0, $"Erro ao enviar email: {ex.Message}");
        }
    }

    // Helper methods for email body generation (to be implemented when actual email sending is added)
    // private string BuildStatusChangeEmailBody(string name, int requestId, RequestStatus oldStatus, RequestStatus newStatus) { ... }
    // private string BuildNewRequestEmailBody(string name, string email, string eventType, int requestId) { ... }
}