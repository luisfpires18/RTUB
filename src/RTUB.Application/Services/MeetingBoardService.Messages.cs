using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

// Meeting board: the meeting email and the custom push.
public sealed partial class MeetingBoardService
{
    public async Task<EventResult<MeetingEmailDraftDto>> GetEmailDraftAsync(int id, ClaimsPrincipal user)
    {
        var (access, refusal) = await AccessAsync(user);
        if (access is null)
        {
            return EventResult<MeetingEmailDraftDto>.Fail(refusal);
        }

        var (meeting, closed) = await NotifiableAsync(access, id);
        if (meeting is null)
        {
            return EventResult<MeetingEmailDraftDto>.Fail(closed);
        }

        var users = await UsersAsync();
        var audience = await EmailAudienceAsync(meeting, users, forCancellation: false);
        return EventResult<MeetingEmailDraftDto>.Ok(new MeetingEmailDraftDto(
            $"[RTUB] Reunião: {TypeLabel(meeting.Type)} - {meeting.Title}",
            meeting.Statement ?? string.Empty,
            audience.Receiving.Select(u => Person(u, meeting)).ToList(),
            audience.NotReceiving.Select(u => Person(u, meeting)).ToList(),
            EmailTunoOptions(meeting, users).Select(TunoOption).ToList()));
    }

    public async Task<EventResult<MeetingEmailPreviewDto>> PreviewEmailAsync(int id, MeetingEmailPreviewInput input, ClaimsPrincipal user)
    {
        var (access, refusal) = await AccessAsync(user);
        if (access is null)
        {
            return EventResult<MeetingEmailPreviewDto>.Fail(refusal);
        }

        var (meeting, closed) = await NotifiableAsync(access, id);
        if (meeting is null)
        {
            return EventResult<MeetingEmailPreviewDto>.Fail(closed);
        }

        var body = input.Body ?? string.Empty;
        if (body.Length > MaxEmailBodyLength)
        {
            return EventResult<MeetingEmailPreviewDto>.Invalid("body", $"O corpo do email não pode exceder {MaxEmailBodyLength} caracteres.");
        }

        var (senderName, senderPosition) = Sender(meeting, access.User, await UsersAsync());
        var html = await _templates.RenderMeetingNotificationAsync(
            TypeLabel(meeting.Type), meeting.Title, LongDate(meeting.Date), meeting.Location ?? string.Empty, body,
            senderName, SenderCity(), senderPosition, "Exemplo", "Nome Completo");
        return EventResult<MeetingEmailPreviewDto>.Ok(new MeetingEmailPreviewDto(html));
    }

    public async Task<EventResult<MeetingNoticeResultDto>> SendEmailAsync(int id, MeetingEmailInput input, ClaimsPrincipal user)
    {
        var (access, refusal) = await AccessAsync(user);
        if (access is null)
        {
            return EventResult<MeetingNoticeResultDto>.Fail(refusal);
        }

        var (meeting, closed) = await NotifiableAsync(access, id);
        if (meeting is null)
        {
            return EventResult<MeetingNoticeResultDto>.Fail(closed);
        }

        var errors = new Errors();
        var subject = input.Subject?.Trim();
        var body = input.Body ?? string.Empty;
        if (string.IsNullOrEmpty(subject))
        {
            errors.Add("subject", "O assunto é obrigatório.");
        }
        else if (subject.Length > MaxSubjectLength)
        {
            errors.Add("subject", $"O assunto não pode exceder {MaxSubjectLength} caracteres.");
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            errors.Add("body", "O corpo do email é obrigatório.");
        }
        else if (body.Length > MaxEmailBodyLength)
        {
            errors.Add("body", $"O corpo do email não pode exceder {MaxEmailBodyLength} caracteres.");
        }

        var users = await UsersAsync();
        ApplicationUser? tuno = null;
        if (!string.IsNullOrWhiteSpace(input.TunoId))
        {
            tuno = EmailTunoOptions(meeting, users).FirstOrDefault(u => u.Id == input.TunoId);
            if (tuno is null)
            {
                errors.Add("tunoId", "Escolha um Tuno da lista.");
            }
        }

        if (errors.Any)
        {
            return Invalid<MeetingNoticeResultDto>(errors);
        }

        var audience = await EmailAudienceAsync(meeting, users, forCancellation: false);
        if (audience.Receiving.Count == 0)
        {
            return EventResult<MeetingNoticeResultDto>.Invalid("notice", "Nenhum destinatário encontrado.");
        }

        var recipients = audience.Receiving.ToList();
        if (tuno is not null && recipients.All(r => r.Id != tuno.Id))
        {
            recipients.Add(tuno);
        }

        Meeting target = meeting;
        string topic = subject!;
        var (senderName, senderPosition) = Sender(target, access.User, users);
        var (emails, data) = Addresses(recipients);
        var sent = await SendEmailsAsync(async () =>
        {
            var html = await _templates.RenderMeetingNotificationAsync(
                TypeLabel(target.Type), target.Title, LongDate(target.Date), target.Location ?? string.Empty, body,
                senderName, SenderCity(), senderPosition, string.Empty, string.Empty);
            return await _email.SendMeetingNotificationAsync(target.Id, topic, html, emails, data);
        }, target.Id);

        return sent.Error is null
            ? EventResult<MeetingNoticeResultDto>.Ok(new MeetingNoticeResultDto(sent.Count, sent.Warning))
            : EventResult<MeetingNoticeResultDto>.Invalid("notice", sent.Error);
    }

    public async Task<EventResult<MeetingPushDraftDto>> GetPushDraftAsync(int id, ClaimsPrincipal user)
    {
        var (access, refusal) = await AccessAsync(user);
        if (access is null)
        {
            return EventResult<MeetingPushDraftDto>.Fail(refusal);
        }

        var (meeting, closed) = await NotifiableAsync(access, id);
        if (meeting is null)
        {
            return EventResult<MeetingPushDraftDto>.Fail(closed);
        }

        var users = await UsersAsync();
        var audience = await PushAudienceAsync(meeting, users);
        return EventResult<MeetingPushDraftDto>.Ok(new MeetingPushDraftDto(
            audience.Receiving.Select(u => Person(u, meeting)).ToList(),
            audience.NotReceiving.Select(u => Person(u, meeting)).ToList(),
            PushTunoOptions(meeting, users).Select(TunoOption).ToList()));
    }

    public async Task<EventResult<MeetingNoticeResultDto>> SendPushAsync(int id, MeetingPushInput input, ClaimsPrincipal user)
    {
        var (access, refusal) = await AccessAsync(user);
        if (access is null)
        {
            return EventResult<MeetingNoticeResultDto>.Fail(refusal);
        }

        var (meeting, closed) = await NotifiableAsync(access, id);
        if (meeting is null)
        {
            return EventResult<MeetingNoticeResultDto>.Fail(closed);
        }

        var message = input.Message?.Trim();
        if (string.IsNullOrEmpty(message))
        {
            return EventResult<MeetingNoticeResultDto>.Invalid("message", "A mensagem da notificação é obrigatória.");
        }

        if (message.Length > MaxPushLength)
        {
            return EventResult<MeetingNoticeResultDto>.Invalid("message", $"A mensagem não pode exceder {MaxPushLength} caracteres.");
        }

        var users = await UsersAsync();
        ApplicationUser? tuno = null;
        if (!string.IsNullOrWhiteSpace(input.TunoId))
        {
            tuno = PushTunoOptions(meeting, users).FirstOrDefault(u => u.Id == input.TunoId);
            if (tuno is null)
            {
                return EventResult<MeetingNoticeResultDto>.Invalid("tunoId", "Escolha um Tuno da lista.");
            }
        }

        var audience = await PushAudienceAsync(meeting, users);
        if (audience.Receiving.Count == 0)
        {
            return EventResult<MeetingNoticeResultDto>.Invalid("notice", "Nenhum membro com subscrição push ativa encontrado.");
        }

        var recipients = audience.Receiving.ToList();
        if (tuno is not null && recipients.All(r => r.Id != tuno.Id))
        {
            recipients.Add(tuno);
        }

        var notification = _pushFactory.CreateMeetingCustomNotification(meeting, message);
        var sentCount = 0;
        foreach (var recipient in recipients)
        {
            try
            {
                await _push.SendToUserAsync(recipient.Id, notification);
                sentCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send push notification to user {UserId} for meeting {MeetingId}", recipient.Id, meeting.Id);
            }
        }

        _logger.LogInformation("Sent push notification for meeting {MeetingId} to {SentCount}/{TotalCount} recipients",
            meeting.Id, sentCount, recipients.Count);

        // Audited as the old page did; an audit failure never turns a sent notice into an error.
        var userName = user.Identity?.Name;
        if (!string.IsNullOrEmpty(userName))
        {
            try
            {
                await _audit.AddAsync(new AuditLog
                {
                    EntityType = "Meeting",
                    EntityId = meeting.Id,
                    Action = "PushNotificationSent",
                    UserId = access.UserId,
                    UserName = userName,
                    Timestamp = DateTime.UtcNow,
                    Changes = JsonSerializer.Serialize(new { RecipientCount = sentCount, NotificationBody = message }),
                    EntityDisplayName = meeting.Title,
                    IsCriticalAction = false,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create audit log for push notification sent to meeting {MeetingId}", meeting.Id);
            }
        }

        return EventResult<MeetingNoticeResultDto>.Ok(new MeetingNoticeResultDto(sentCount, null));
    }
}
