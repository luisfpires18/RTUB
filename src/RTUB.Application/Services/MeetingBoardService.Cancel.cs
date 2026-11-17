using System.Security.Claims;
using RTUB.Application.DTOs;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

// Meeting board: cancel and uncancel a meeting.
public sealed partial class MeetingBoardService
{
    public async Task<EventResult<MeetingCancelDraftDto>> GetCancelDraftAsync(int id, ClaimsPrincipal user)
    {
        var (access, refusal) = await AccessAsync(user);
        if (access is null)
        {
            return EventResult<MeetingCancelDraftDto>.Fail(refusal);
        }

        var (meeting, closed) = await NotifiableAsync(access, id);
        if (meeting is null)
        {
            return EventResult<MeetingCancelDraftDto>.Fail(closed);
        }

        var audience = await EmailAudienceAsync(meeting, await UsersAsync(), forCancellation: true);
        return EventResult<MeetingCancelDraftDto>.Ok(new MeetingCancelDraftDto(
            audience.Receiving.Select(u => Person(u, meeting)).ToList(),
            audience.NotReceiving.Select(u => Person(u, meeting)).ToList(),
            audience.Receiving.Count + audience.NotReceiving.Count));
    }

    public async Task<EventResult<MeetingNoticeResultDto>> CancelAsync(int id, MeetingCancelInput input, ClaimsPrincipal user)
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

        var reason = input.Reason?.Trim();
        if (string.IsNullOrEmpty(reason))
        {
            return EventResult<MeetingNoticeResultDto>.Invalid("reason", "O motivo do cancelamento é obrigatório.");
        }

        if (reason.Length > MaxReasonLength)
        {
            return EventResult<MeetingNoticeResultDto>.Invalid("reason", $"O motivo não pode exceder {MaxReasonLength} caracteres.");
        }

        meeting.IsCancelled = true;
        meeting.CancellationReason = reason;
        await _meetings.UpdateMeetingAsync(meeting);

        if (!input.NotifyByEmail)
        {
            return EventResult<MeetingNoticeResultDto>.Ok(new MeetingNoticeResultDto(0, null));
        }

        var users = await UsersAsync();
        var audience = await EmailAudienceAsync(meeting, users, forCancellation: true);
        if (audience.Receiving.Count == 0)
        {
            return EventResult<MeetingNoticeResultDto>.Ok(new MeetingNoticeResultDto(0, null));
        }

        // The meeting is cancelled whatever happens to the emails; a failure is reported, not thrown.
        Meeting cancelled = meeting;
        string why = reason;
        var (senderName, senderPosition) = Sender(cancelled, access.User, users);
        var (emails, data) = Addresses(audience.Receiving);
        var sent = await SendEmailsAsync(async () =>
        {
            var body = await _templates.RenderMeetingCancellationAsync(
                TypeLabel(cancelled.Type), cancelled.Title, LongDate(cancelled.Date), cancelled.Location ?? string.Empty, why,
                senderName, SenderCity(), senderPosition, string.Empty, string.Empty);
            return await _email.SendMeetingNotificationAsync(
                cancelled.Id, $"[RTUB] Cancelamento: {TypeLabel(cancelled.Type)} - {cancelled.Title}", body, emails, data);
        }, cancelled.Id);

        return EventResult<MeetingNoticeResultDto>.Ok(sent.Error is null
            ? new MeetingNoticeResultDto(sent.Count, sent.Warning)
            : new MeetingNoticeResultDto(0, $"A reunião foi cancelada, mas os emails não foram enviados: {sent.Error}"));
    }

    public async Task<EventResult<MeetingCardDto>> UncancelAsync(int id, ClaimsPrincipal user)
    {
        var (access, refusal) = await AccessAsync(user);
        if (access is null)
        {
            return EventResult<MeetingCardDto>.Fail(refusal);
        }

        var meeting = await VisibleMeetingAsync(access, id);
        if (meeting is null)
        {
            return EventResult<MeetingCardDto>.Fail(EventResultStatus.NotFound);
        }

        if (!access.CanManage(meeting))
        {
            return EventResult<MeetingCardDto>.Fail(EventResultStatus.Forbidden);
        }

        if (!meeting.IsCancelled)
        {
            return EventResult<MeetingCardDto>.Fail(EventResultStatus.Closed);
        }

        meeting.IsCancelled = false;
        meeting.CancellationReason = null;
        await _meetings.UpdateMeetingAsync(meeting);
        return EventResult<MeetingCardDto>.Ok(await CardAsync(access, meeting));
    }
}
