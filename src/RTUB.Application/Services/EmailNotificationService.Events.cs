using System.Text;
using Microsoft.Extensions.Logging;
using RTUB.Application.DTOs;
using RTUB.Application.Extensions;
using RTUB.Application.Services.Email;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

// Email notifications: events (new, cancelled, reminder).
public partial class EmailNotificationService
{
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
        DateTime? endDate = null,
        IProgress<EmailSendProgress>? progress = null)
    {
        // Rate limiting: Prevents sending duplicate emails within a short time window
        // The EmailRateLimiter tracks recent sends by key and returns true if we should skip
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

            // Batching: When recipient data is provided, emails are sent in batches with personalization
            // This happens in SendPersonalizedBatchAsync which handles concurrent sending with SemaphoreSlim
            if (recipientData is not null && recipientData.Any())
            {
                return await SendPersonalizedBatchAsync(config, recipientEmails, recipientData, subject,
                    async (nickname, fullName) => await _templateRenderer.RenderEventNotificationAsync(
                        eventTitle, eventDate, endDate, eventLocation, eventLink, nickname, fullName, eventDescription),
                    eventId, "event notification", progress);
            }

            // Non-personalized (BCC mode)
            var body = await _templateRenderer.RenderEventNotificationAsync(eventTitle, eventDate, endDate, eventLocation, eventLink, "", "", eventDescription);
            return await SendBccEmailAsync(config, recipientEmails, subject, body, eventId, "event notification");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending event notification email for event {EventId}", eventId);
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
        Dictionary<string, (string nickname, string fullName)>? recipientData = null,
        DateTime? endDate = null,
        IProgress<EmailSendProgress>? progress = null)
    {
        // Rate limiting: Prevents multiple cancellation emails for the same event
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

            return await SendPersonalizedBatchAsync(config, recipientEmails, recipientData, subject,
                async (nickname, fullName) => await _templateRenderer.RenderEventCancellationNotificationAsync(
                    eventTitle, eventDate, endDate, eventLocation, cancellationReason, eventLink, nickname, fullName),
                eventId, "event cancellation notification", progress);
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
        DateTime? endDate = null,
        IProgress<EmailSendProgress>? progress = null)
    {
        // Rate limiting: Prevents duplicate reminder emails within a short time window
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

            // Load confirmed enrollments for this event
            var enrollments = await _enrollmentService.GetEnrollmentsByEventIdAsync(eventId);
            var confirmedEnrollments = enrollments.Where(e => e.WillAttend && e.User != null).ToList();

            // Build participants list with display name, category, instrument, and notes
            var participants = confirmedEnrollments.Select(e =>
            {
                var user = e.User!;
                var displayName = user.GetDisplayName();
                var category = user.GetPrimaryCategoryName();
                var instrument = e.Instrument.HasValue
                    ? InstrumentTypeHelper.GetDisplayName(e.Instrument.Value)
                    : "Não especificado";
                var isLeitao = user.IsLeitao();

                return (displayName, category, instrument, e.Notes, isLeitao);
            }).ToList();

            // Load repertoire songs for this event
            // For single-day events: get repertoire for that specific day
            // For date range events: get repertoire for all days in the range
            var repertoireSongs = new List<(string title, DateTime repertoireDate)>();
            var repertoireItems = await _eventRepertoireService.GetRepertoireByEventIdAsync(eventId);

            if (repertoireItems.Any())
            {
                repertoireSongs = repertoireItems
                    .Where(r => r.Song != null)
                    .Select(r => (
                        title: r.Song!.Title,
                        repertoireDate: r.RepertoireDate
                    ))
                    .ToList();
            }

            var daysUntilEvent = (int)Math.Ceiling((eventDate.Date - DateTime.UtcNow.Date).TotalDays);
            var subject = $"Lembrete: {eventTitle}, {(daysUntilEvent == 1 ? $"falta {daysUntilEvent} dia" : $"faltam {daysUntilEvent} dias")}";

            return await SendPersonalizedBatchAsync(config, recipientEmails, recipientData, subject,
                async (nickname, fullName) => await _templateRenderer.RenderEventReminderNotificationAsync(
                    eventTitle, eventDate, endDate, eventLocation, eventLink, daysUntilEvent, nickname, fullName, eventDescription, participants, repertoireSongs),
                eventId, "event reminder notification", progress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending event reminder notification email for event {EventId}", eventId);
            return (false, 0, $"Erro ao enviar email: {ex.Message}");
        }
    }
}
