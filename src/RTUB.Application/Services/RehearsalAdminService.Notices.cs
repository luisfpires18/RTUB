// Rehearsal admin: notice audience and sending notices.
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

public sealed partial class RehearsalAdminService
{
    public async Task<EventResult<RehearsalNoticeAudienceDto>> GetNoticeAudienceAsync(int id, ClaimsPrincipal user)
    {
        if (await RefusalAsync<RehearsalNoticeAudienceDto>(id, user) is { } refused)
        {
            return refused;
        }

        var (all, few) = await AudienceAsync();
        return EventResult<RehearsalNoticeAudienceDto>.Ok(new RehearsalNoticeAudienceDto(
            all.Count(u => u.Subscribed), all.Count, few.Count(u => u.Subscribed), few.Count));
    }

    public async Task<EventResult<RehearsalNoticeResultDto>> SendNoticeAsync(int id, RehearsalNoticeInput input, ClaimsPrincipal user)
    {
        if (await RefusalAsync<RehearsalNoticeResultDto>(id, user) is { } refused)
        {
            return refused;
        }

        var r = await FindAsync(id);
        if (r!.IsCanceled || RehearsalAgendaService.IsPast(r, Today))
        {
            return EventResult<RehearsalNoticeResultDto>.Fail(EventResultStatus.Closed);
        }

        var message = RehearsalAgendaService.Blank(input.Message);
        if (message is null || message.Length > NoticeMax)
        {
            return EventResult<RehearsalNoticeResultDto>.Invalid("message", message is null ? "A mensagem da notificação é obrigatória." : $"A mensagem não pode exceder {NoticeMax} caracteres.");
        }

        var (all, few) = await AudienceAsync();
        var recipients = (input.OnlyLeitoesAndCaloiros ? few : all).Where(u => u.Subscribed).Select(u => u.Id).ToList();
        if (recipients.Count == 0)
        {
            return EventResult<RehearsalNoticeResultDto>.Invalid("message", "Ninguém deste grupo tem as notificações ativas.");
        }

        var notification = _pushFactory.CreateRehearsalNotification(r, message);
        var (sent, failed) = await _push.SendToSelectedUsersAsync(recipients, notification);

        try
        {
            await _audit.AddAsync(new AuditLog
            {
                EntityType = "Rehearsal",
                EntityId = r.Id,
                Action = "PushNotificationSent",
                UserId = RehearsalsAuthorization.UserId(user),
                UserName = user.Identity!.Name,
                Timestamp = DateTime.UtcNow,
                Changes = JsonSerializer.Serialize(new { RecipientCount = recipients.Count, NotificationBody = message }),
                EntityDisplayName = $"Ensaio - {r.Date:dd/MM/yyyy} {(string.IsNullOrEmpty(r.Theme) ? r.Location : r.Theme)}",
                IsCriticalAction = false,
            });
        }
        catch (Exception ex)
        {
            // The notice goes out even if its audit row could not be written.
            _logger.LogError(ex, "Failed to audit a push notice for rehearsal {RehearsalId}", r.Id);
        }

        return EventResult<RehearsalNoticeResultDto>.Ok(new RehearsalNoticeResultDto(sent, failed));
    }
}
