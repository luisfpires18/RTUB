using Microsoft.Extensions.Logging;
using RTUB.Application.DTOs;

namespace RTUB.Application.Services;

// Email notifications: new request, welcome, birthday and username change.
public partial class EmailNotificationService
{
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

            if (!_configProvider.ValidateSenderEmail(config))
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

            await SendSingleEmailAsync(config, RequestNotificationRecipient, subject, body);
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
    public async Task<(bool success, int count, string? errorMessage)> SendBirthdayNotificationAsync(
        string birthdayPersonId,
        string birthdayPersonNickname,
        string birthdayPersonFullName,
        List<string> recipientEmails,
        Dictionary<string, (string nickname, string fullName)> recipientData,
        IProgress<EmailSendProgress>? progress = null)
    {
        // Rate limiting: Uses a daily key to prevent sending birthday emails multiple times per day
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
                birthdayPersonId, "birthday notification", progress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending birthday notification email for user {UserName}", birthdayPersonId);
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
}
