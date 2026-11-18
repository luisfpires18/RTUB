// Event admin: notices to enrolled members (email and push).
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RTUB.Application.DTOs;
using RTUB.Application.Extensions;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

public sealed partial class EventAdminService
{
    // ---------- notices ----------

    public async Task<EventResult<EventNoticeAudienceDto>> GetNoticeAudienceAsync(int id, ClaimsPrincipal user)
    {
        if (Refusal<EventNoticeAudienceDto>(user) is { } refused)
        {
            return refused;
        }

        if (await FindAsync(id) is null)
        {
            return EventResult<EventNoticeAudienceDto>.Fail(EventResultStatus.NotFound);
        }

        var users = await UsersAsync();
        var pushIds = (await _push.GetSubscribedUserIdsAsync()).ToHashSet();
        var young = users.Where(u => u.IsLeitao() || u.IsCaloiro()).ToList();
        return EventResult<EventNoticeAudienceDto>.Ok(new EventNoticeAudienceDto(
            users.Count(u => u.Subscribed && u.EmailConfirmed && !string.IsNullOrWhiteSpace(u.Email)),
            users.Count(u => u.EmailConfirmed),
            users.Count(u => pushIds.Contains(u.Id)),
            users.Count,
            young.Count(u => pushIds.Contains(u.Id)),
            young.Count));
    }

    public async Task<EventResult<EventNoticeResultDto>> SendNoticeAsync(int id, EventNoticeInput input, ClaimsPrincipal user, string baseUrl)
    {
        if (Refusal<EventNoticeResultDto>(user) is { } refused)
        {
            return refused;
        }

        var e = await FindAsync(id);
        if (e is null)
        {
            return EventResult<EventNoticeResultDto>.Fail(EventResultStatus.NotFound);
        }

        if (e.IsCancelled || IsPast(e, Today))
        {
            return EventResult<EventNoticeResultDto>.Fail(EventResultStatus.Closed);
        }

        return input.Channel switch
        {
            "email" => await SendEmailNoticeAsync(e, input, baseUrl),
            "push" => await SendPushNoticeAsync(e, input, user),
            _ => EventResult<EventNoticeResultDto>.Invalid("channel", "Escolha email ou push."),
        };
    }

    private async Task<EventResult<EventNoticeResultDto>> SendEmailNoticeAsync(Event e, EventNoticeInput input, string baseUrl)
    {
        if (input.Kind is not ("new" or "reminder"))
        {
            return EventResult<EventNoticeResultDto>.Invalid("kind", "Escolha o tipo de email.");
        }

        var recipients = await EmailRecipientsAsync();
        if (recipients.Emails.Count == 0)
        {
            return EventResult<EventNoticeResultDto>.Invalid("notice", "Nenhum membro tem as notificações por email ativas.");
        }

        var link = EventsLink(baseUrl);
        var sent = await SendEmailAsync(() => input.Kind == "reminder"
            ? _email.SendEventReminderNotificationAsync(e.Id, e.Name, e.Date, e.Location, link, recipients.Emails, recipients.Data, e.Description, e.EndDate)
            : _email.SendEventNotificationAsync(e.Id, e.Name, e.Date, e.Location, link, recipients.Emails, recipients.Data, e.Description, e.EndDate), e.Id);

        // The email service's own words (e.g. "already sent recently" from its rate limit) reach the modal.
        return sent.Error is null
            ? EventResult<EventNoticeResultDto>.Ok(new EventNoticeResultDto(sent.Count, recipients.Emails.Count - sent.Count, sent.Warning))
            : EventResult<EventNoticeResultDto>.Invalid("notice", sent.Error);
    }

    private async Task<EventResult<EventNoticeResultDto>> SendPushNoticeAsync(Event e, EventNoticeInput input, ClaimsPrincipal user)
    {
        var message = input.Message?.Trim();
        if (string.IsNullOrEmpty(message))
        {
            return EventResult<EventNoticeResultDto>.Invalid("message", "A mensagem da notificação é obrigatória.");
        }

        if (message.Length > MaxPushLength)
        {
            return EventResult<EventNoticeResultDto>.Invalid("message", $"A mensagem não pode exceder {MaxPushLength} caracteres.");
        }

        var pushIds = (await _push.GetSubscribedUserIdsAsync()).ToHashSet();
        var recipients = (await UsersAsync())
            .Where(u => pushIds.Contains(u.Id) && (!input.OnlyLeitoesAndCaloiros || u.IsLeitao() || u.IsCaloiro()))
            .Select(u => u.Id)
            .ToList();
        if (recipients.Count == 0)
        {
            return EventResult<EventNoticeResultDto>.Invalid("notice", "Nenhum membro deste público tem as notificações push ativas.");
        }

        var (sent, failed) = await _push.SendToSelectedUsersAsync(recipients, _pushFactory.CreateEventCustomNotification(e, message));

        // Audited; an audit failure never turns a sent notice into an error.
        try
        {
            await _audit.AddAsync(new AuditLog
            {
                EntityType = "Event",
                EntityId = e.Id,
                Action = "PushNotificationSent",
                UserId = user.UserId(),
                UserName = user.Identity?.Name,
                Timestamp = DateTime.UtcNow,
                Changes = JsonSerializer.Serialize(new { RecipientCount = recipients.Count, NotificationBody = message }),
                EntityDisplayName = e.Name,
                IsCriticalAction = false,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to audit a push notice for event {EventId}", e.Id);
        }

        return EventResult<EventNoticeResultDto>.Ok(new EventNoticeResultDto(sent, failed,
            failed > 0 ? "Algumas notificações não foram entregues." : null));
    }
}
