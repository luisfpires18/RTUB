using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Extensions;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

/// <summary>
/// The React /meetings (task 034, docs/react-meetings.md; was the Blazor page). The old page only hid buttons and its
/// services checked nothing, so every gate it had is enforced here, through <see cref="MeetingAccess"/>:
///
/// - Visitors: <c>SignInRequired</c>. A Leitão: <c>Forbidden</c> on every call, whatever their role.
/// - Which meetings a member sees is <see cref="IMeetingService.GetAllMeetingsAsync"/>'s filter, unchanged (a Tuno
///   representative sees their CV meeting, decision A5). A meeting the member does not see is <c>NotFound</c>, for reads
///   and writes alike, so nothing tells them it exists.
/// - Draft atas are for whoever may write them; published ones follow the old reading rule (decision A1, the one
///   security change). Everything else is the old page's effective behaviour (decisions A2-A7).
///
/// Side effects are the old page's, through the same services: push on a new meeting or request, on a rejected request
/// and on a reminder; the meeting email and the cancellation email; the custom push (audited); the ata PDF in
/// Documentation on publish. Lengths are enforced here (the old forms declared them but never ran them).
/// </summary>
public sealed class MeetingBoardService : IMeetingBoardService
{
    public const int MaxTitleLength = 200;
    public const int MaxLocationLength = 200;
    public const int MaxStatementLength = 5000;
    public const int MaxDescriptionLength = 2000;
    public const int MaxNotesLength = 500;
    public const int MaxReasonLength = 1000;
    public const int MaxPushLength = 500;
    public const int MaxSubjectLength = 300;
    public const int MaxEmailBodyLength = 10000;
    public const int MaxAtaNumberLength = 50;
    public const int MaxClosingTextLength = 5000;
    public const int MaxAgendaTitleLength = 500;
    public const int MaxAgendaTextLength = 5000;
    public const int MaxAgendaPoints = 100;
    public const int MaxVotes = 100000;
    public const int MaxCandidates = 50;

    private const string DateFormat = "yyyy-MM-dd'T'HH:mm";
    private static readonly string[] DateFormats = { "yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd'T'HH:mm:ss" };
    private static readonly CultureInfo Portuguese = new("pt-PT");
    private static readonly int[] RequestPageSizes = { 4, 8, 12, 16, 20 };
    private static readonly string[] QuorumBases = { "HoraAgendada", "HoraAgendadaMais30Minutos" };
    private static readonly string[] VoteResults = { "Aprovado", "Rejeitado" };
    private static readonly RequestStatus[] RequestFilters = { RequestStatus.Pending, RequestStatus.Confirmed, RequestStatus.Rejected };

    private readonly IDbContextFactory<ApplicationDbContext> _contexts;
    private readonly IMeetingService _meetings;
    private readonly IMeetingRequestService _requests;
    private readonly IMeetingParticipationService _participations;
    private readonly IMeetingAtaService _atas;
    private readonly IMeetingAtaConfirmationService _confirmations;
    private readonly IAtaPdfService _pdf;
    private readonly IDocumentStorageService _documents;
    private readonly IEmailNotificationService _email;
    private readonly IEmailTemplateRenderer _templates;
    private readonly IPushNotificationService _push;
    private readonly IPushNotificationFactory _pushFactory;
    private readonly IAuditLogService _audit;
    private readonly IFiscalYearService _fiscalYears;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<MeetingBoardService> _logger;

    public MeetingBoardService(
        IDbContextFactory<ApplicationDbContext> contexts,
        IMeetingService meetings,
        IMeetingRequestService requests,
        IMeetingParticipationService participations,
        IMeetingAtaService atas,
        IMeetingAtaConfirmationService confirmations,
        IAtaPdfService pdf,
        IDocumentStorageService documents,
        IEmailNotificationService email,
        IEmailTemplateRenderer templates,
        IPushNotificationService push,
        IPushNotificationFactory pushFactory,
        IAuditLogService audit,
        IFiscalYearService fiscalYears,
        IHostEnvironment environment,
        ILogger<MeetingBoardService> logger)
    {
        _contexts = contexts;
        _meetings = meetings;
        _requests = requests;
        _participations = participations;
        _atas = atas;
        _confirmations = confirmations;
        _pdf = pdf;
        _documents = documents;
        _email = email;
        _templates = templates;
        _push = push;
        _pushFactory = pushFactory;
        _audit = audit;
        _fiscalYears = fiscalYears;
        _environment = environment;
        _logger = logger;
    }

    // ---------- meetings ----------

    public async Task<EventResult<MeetingBoardDto>> GetBoardAsync(string? fiscalYear, string? search, ClaimsPrincipal user)
    {
        var (access, refusal) = await AccessAsync(user);
        if (access is null)
        {
            return EventResult<MeetingBoardDto>.Fail(refusal);
        }

        if (!TryFiscalYear(fiscalYear, out var selected, out var range))
        {
            return EventResult<MeetingBoardDto>.Invalid("fy", "Escolha um ano da lista.");
        }

        IEnumerable<Meeting> meetings = await VisibleMeetingsAsync(access);
        if (range is { } r)
        {
            // The old filter, kept as it was: from 1 September to 31 August at midnight.
            meetings = meetings.Where(m => m.Date >= r.Start && m.Date <= r.End);
        }

        // In memory: the old search ran a case-insensitive Contains inside the SQL query.
        var q = search?.Trim();
        if (!string.IsNullOrEmpty(q))
        {
            meetings = meetings.Where(m => m.Title.Contains(q, StringComparison.OrdinalIgnoreCase)
                                           || (m.Statement ?? string.Empty).Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        var today = DateTime.Today;
        var list = meetings.ToList();
        var upcoming = list.Where(m => m.Date.Date >= today).OrderBy(m => m.Date).ToList();
        var past = list.Where(m => m.Date.Date < today).OrderByDescending(m => m.Date).ToList();
        var cards = await CardsAsync(access, upcoming.Concat(past).ToList());

        var years = (await _fiscalYears.GetAllFiscalYearsAsync())
            .Where(fy => fy.StartYear >= FiscalYearHelper.AppYearCreated)
            .Select(fy => fy.GetFiscalYearString())
            .OrderByDescending(y => y, StringComparer.Ordinal)
            .ToList();

        return EventResult<MeetingBoardDto>.Ok(new MeetingBoardDto(
            cards.Take(upcoming.Count).ToList(),
            cards.Skip(upcoming.Count).ToList(),
            years,
            FiscalYearHelper.GetCurrentFiscalYearString(),
            selected,
            new MeetingBoardAccessDto(
                access.CanCreateMeetings,
                TypeOptions(access.FormTypes),
                access.CanProposeCv,
                access.CanProposeDirecao,
                access.CanProposeAg,
                access.CanSeeRequests,
                access.CanAddParticipants)));
    }

    public async Task<EventResult<MeetingCardDto>> GetMeetingAsync(int id, ClaimsPrincipal user)
    {
        var (access, refusal) = await AccessAsync(user);
        if (access is null)
        {
            return EventResult<MeetingCardDto>.Fail(refusal);
        }

        var meeting = await VisibleMeetingAsync(access, id);
        return meeting is null
            ? EventResult<MeetingCardDto>.Fail(EventResultStatus.NotFound)
            : EventResult<MeetingCardDto>.Ok(await CardAsync(access, meeting));
    }

    public async Task<EventResult<MeetingFormDto>> GetFormAsync(ClaimsPrincipal user)
    {
        var (access, refusal) = await AccessAsync(user);
        if (access is null)
        {
            return EventResult<MeetingFormDto>.Fail(refusal);
        }

        if (!access.CanCreateMeetings)
        {
            return EventResult<MeetingFormDto>.Fail(EventResultStatus.Forbidden);
        }

        var users = await UsersAsync();
        return EventResult<MeetingFormDto>.Ok(new MeetingFormDto(
            TypeOptions(access.FormTypes),
            TunoRepresentativeCandidates(users).Select(TunoOption).ToList()));
    }

    public async Task<EventResult<MeetingSavedDto>> CreateAsync(MeetingInput input, ClaimsPrincipal user)
    {
        var (access, refusal) = await AccessAsync(user);
        if (access is null)
        {
            return EventResult<MeetingSavedDto>.Fail(refusal);
        }

        if (!access.CanCreateMeetings)
        {
            return EventResult<MeetingSavedDto>.Fail(EventResultStatus.Forbidden);
        }

        var (meeting, errors) = ReadMeeting(input, access, await UsersAsync(), savedRepresentativeId: null);
        if (meeting is null)
        {
            return Invalid<MeetingSavedDto>(errors);
        }

        meeting.OrganizerUserId = access.UserId;

        // The old service push to the meeting type's audience comes with it.
        var created = await _meetings.CreateMeetingAsync(meeting);
        return EventResult<MeetingSavedDto>.Ok(new MeetingSavedDto(created.Id, await CardOrNullAsync(access, created.Id)));
    }

    public async Task<EventResult<MeetingSavedDto>> UpdateAsync(int id, MeetingInput input, ClaimsPrincipal user)
    {
        var (access, refusal) = await AccessAsync(user);
        if (access is null)
        {
            return EventResult<MeetingSavedDto>.Fail(refusal);
        }

        var existing = await VisibleMeetingAsync(access, id);
        if (existing is null)
        {
            return EventResult<MeetingSavedDto>.Fail(EventResultStatus.NotFound);
        }

        if (!access.CanManage(existing))
        {
            return EventResult<MeetingSavedDto>.Fail(EventResultStatus.Forbidden);
        }

        var (meeting, errors) = ReadMeeting(input, access, await UsersAsync(), existing.TunoRepresentativeUserId);
        if (meeting is null)
        {
            return Invalid<MeetingSavedDto>(errors);
        }

        // The organizer stays; a cancelled meeting stays cancelled (the old edit form silently reactivated it).
        meeting.Id = existing.Id;
        meeting.OrganizerUserId = existing.OrganizerUserId;
        meeting.IsCancelled = existing.IsCancelled;
        meeting.CancellationReason = existing.CancellationReason;
        await _meetings.UpdateMeetingAsync(meeting);

        return EventResult<MeetingSavedDto>.Ok(new MeetingSavedDto(existing.Id, await CardOrNullAsync(access, existing.Id)));
    }

    public async Task<EventResult<bool>> DeleteAsync(int id, ClaimsPrincipal user)
    {
        var (access, refusal) = await AccessAsync(user);
        if (access is null)
        {
            return EventResult<bool>.Fail(refusal);
        }

        var meeting = await VisibleMeetingAsync(access, id);
        if (meeting is null)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        if (!access.CanManage(meeting))
        {
            return EventResult<bool>.Fail(EventResultStatus.Forbidden);
        }

        await _meetings.DeleteMeetingAsync(meeting.Id);
        return EventResult<bool>.Ok(true);
    }

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

    public async Task<EventResult<MeetingNoticeResultDto>> SendPushAsync(int id, MeetingPushInput input, ClaimsPrincipal user, string baseUrl)
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

        var notification = _pushFactory.CreateMeetingCustomNotification(meeting, message, baseUrl);
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

    // ---------- participations ----------

    public async Task<EventResult<MeetingCardDto>> RespondAsync(int id, MeetingParticipationInput input, ClaimsPrincipal user)
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

        // "Vou / Não vou" only before the day of a meeting that is not cancelled, as on the old card.
        if (meeting.IsCancelled || meeting.Date.Date <= DateTime.Today)
        {
            return EventResult<MeetingCardDto>.Fail(EventResultStatus.Closed);
        }

        var notes = NullIfBlank(input.Notes);
        if (notes?.Length > MaxNotesLength)
        {
            return EventResult<MeetingCardDto>.Invalid("notes", $"A nota não pode exceder {MaxNotesLength} caracteres.");
        }

        var existing = await _participations.GetParticipationByMeetingAndUserAsync(meeting.Id, access.UserId);
        if (existing is null)
        {
            await _participations.CreateParticipationAsync(access.UserId, meeting.Id, notes, input.WillAttend);
        }
        else
        {
            await _participations.UpdateParticipationAsync(existing.Id, input.WillAttend, notes);
        }

        return EventResult<MeetingCardDto>.Ok(await CardAsync(access, meeting));
    }

    public async Task<EventResult<MeetingParticipantsDto>> GetParticipantsAsync(int id, ClaimsPrincipal user)
    {
        var (access, refusal) = await AccessAsync(user);
        if (access is null)
        {
            return EventResult<MeetingParticipantsDto>.Fail(refusal);
        }

        var (meeting, closed) = await ParticipantsMeetingAsync(access, id);
        return meeting is null
            ? EventResult<MeetingParticipantsDto>.Fail(closed)
            : EventResult<MeetingParticipantsDto>.Ok(await ParticipantsAsync(access, meeting));
    }

    public async Task<EventResult<MeetingParticipantsDto>> RemoveParticipationAsync(int id, int participationId, ClaimsPrincipal user)
    {
        var (access, refusal) = await AccessAsync(user);
        if (access is null)
        {
            return EventResult<MeetingParticipantsDto>.Fail(refusal);
        }

        var (meeting, closed) = await ParticipantsMeetingAsync(access, id);
        if (meeting is null)
        {
            return EventResult<MeetingParticipantsDto>.Fail(closed);
        }

        var participation = await _participations.GetParticipationByIdAsync(participationId);
        if (participation is null || participation.MeetingId != meeting.Id)
        {
            return EventResult<MeetingParticipantsDto>.Fail(EventResultStatus.NotFound);
        }

        if (!access.CanRemoveParticipation(participation))
        {
            return EventResult<MeetingParticipantsDto>.Fail(EventResultStatus.Forbidden);
        }

        await _participations.DeleteParticipationAsync(participation.Id);
        return EventResult<MeetingParticipantsDto>.Ok(await ParticipantsAsync(access, meeting));
    }

    public async Task<EventResult<IReadOnlyList<MeetingCandidateDto>>> GetCandidatesAsync(int id, string? search, ClaimsPrincipal user)
    {
        var (access, refusal) = await AccessAsync(user);
        if (access is null)
        {
            return EventResult<IReadOnlyList<MeetingCandidateDto>>.Fail(refusal);
        }

        var (meeting, closed) = await ParticipantsMeetingAsync(access, id);
        if (meeting is null)
        {
            return EventResult<IReadOnlyList<MeetingCandidateDto>>.Fail(closed);
        }

        if (!access.CanAddParticipants)
        {
            return EventResult<IReadOnlyList<MeetingCandidateDto>>.Fail(EventResultStatus.Forbidden);
        }

        var q = search?.Trim();
        if (string.IsNullOrEmpty(q))
        {
            return EventResult<IReadOnlyList<MeetingCandidateDto>>.Ok(Array.Empty<MeetingCandidateDto>());
        }

        var taken = (await _participations.GetParticipationsByMeetingIdAsync(meeting.Id)).Select(p => p.UserId).ToHashSet();
        var candidates = (await UsersAsync())
            .Where(u => !taken.Contains(u.Id) && MeetingAccess.CanBeAddedToMeeting(u))
            .Where(u => (u.FirstName?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                        || (u.LastName?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                        || (u.Nickname?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderBy(u => u.FirstName)
            .Take(MaxCandidates)
            .Select(u => new MeetingCandidateDto(u.Id, u.GetDisplayName(), u.ProfilePictureSrc))
            .ToList();
        return EventResult<IReadOnlyList<MeetingCandidateDto>>.Ok(candidates);
    }

    public async Task<EventResult<MeetingParticipantsDto>> AddParticipantAsync(int id, MeetingAddParticipantInput input, ClaimsPrincipal user)
    {
        var (access, refusal) = await AccessAsync(user);
        if (access is null)
        {
            return EventResult<MeetingParticipantsDto>.Fail(refusal);
        }

        var (meeting, closed) = await ParticipantsMeetingAsync(access, id);
        if (meeting is null)
        {
            return EventResult<MeetingParticipantsDto>.Fail(closed);
        }

        if (!access.CanAddParticipants)
        {
            return EventResult<MeetingParticipantsDto>.Fail(EventResultStatus.Forbidden);
        }

        var target = string.IsNullOrWhiteSpace(input.UserId) ? null : (await UsersAsync()).FirstOrDefault(u => u.Id == input.UserId);
        if (target is null || !MeetingAccess.CanBeAddedToMeeting(target))
        {
            return EventResult<MeetingParticipantsDto>.Invalid("userId", "Escolha um membro da lista.");
        }

        // The old search never offered someone who had already answered ("Vou" or "Não vou"), so their answer and note
        // are never overwritten from here.
        if (await _participations.GetParticipationByMeetingAndUserAsync(meeting.Id, target.Id) is not null)
        {
            return EventResult<MeetingParticipantsDto>.Invalid("userId", "Este membro já respondeu a esta reunião.");
        }

        await _participations.CreateParticipationAsync(target.Id, meeting.Id, null, true);
        return EventResult<MeetingParticipantsDto>.Ok(await ParticipantsAsync(access, meeting));
    }

    // ---------- atas ----------

    public async Task<EventResult<MeetingAtaViewDto>> GetAtaAsync(int id, ClaimsPrincipal user)
    {
        var (access, refusal) = await AccessAsync(user);
        if (access is null)
        {
            return EventResult<MeetingAtaViewDto>.Fail(refusal);
        }

        var (context, closed) = await AtaContextAsync(access, id);
        if (context is null)
        {
            return EventResult<MeetingAtaViewDto>.Fail(closed);
        }

        return ReadableAta(access, context) is { } ata
            ? EventResult<MeetingAtaViewDto>.Ok(await AtaViewAsync(access, ata.Id))
            : EventResult<MeetingAtaViewDto>.Fail(EventResultStatus.NotFound);
    }

    public async Task<EventResult<MeetingAtaEditorDto>> GetAtaEditorAsync(int id, ClaimsPrincipal user)
    {
        var (access, refusal) = await AccessAsync(user);
        if (access is null)
        {
            return EventResult<MeetingAtaEditorDto>.Fail(refusal);
        }

        var (context, closed) = await AtaContextAsync(access, id);
        if (context is null)
        {
            return EventResult<MeetingAtaEditorDto>.Fail(closed);
        }

        return context.CanWrite
            ? EventResult<MeetingAtaEditorDto>.Ok(await AtaEditorAsync(access, context.Meeting, context.Existing))
            : EventResult<MeetingAtaEditorDto>.Fail(EventResultStatus.Forbidden);
    }

    public async Task<EventResult<MeetingAtaEditorDto>> SaveAtaAsync(int id, MeetingAtaInput input, ClaimsPrincipal user)
    {
        var (access, refusal) = await AccessAsync(user);
        if (access is null)
        {
            return EventResult<MeetingAtaEditorDto>.Fail(refusal);
        }

        var (context, closed) = await AtaContextAsync(access, id);
        if (context is null)
        {
            return EventResult<MeetingAtaEditorDto>.Fail(closed);
        }

        if (!context.CanWrite)
        {
            return EventResult<MeetingAtaEditorDto>.Fail(EventResultStatus.Forbidden);
        }

        if (context.Existing?.Status == MeetingAtaStatus.Published)
        {
            return EventResult<MeetingAtaEditorDto>.Fail(EventResultStatus.Closed);
        }

        var meeting = context.Meeting;
        var existing = context.Existing is null ? null : await _atas.GetByIdWithDetailsAsync(context.Existing.Id);
        var setup = await AtaSetupAsync(access, meeting, existing);
        var errors = new Errors();

        var ataNumber = NullIfBlank(input.AtaNumber)?.Trim();
        if (ataNumber?.Length > MaxAtaNumberLength)
        {
            errors.Add("ataNumber", $"O número da Ata não pode exceder {MaxAtaNumberLength} caracteres.");
        }

        if (!TryDate(input.ActualStartTime, out var start))
        {
            errors.Add("actualStartTime", "A hora de início efetiva é obrigatória.");
        }

        DateTime? end = null;
        if (!string.IsNullOrWhiteSpace(input.ActualEndTime))
        {
            if (TryDate(input.ActualEndTime, out var parsedEnd))
            {
                end = parsedEnd;
            }
            else
            {
                errors.Add("actualEndTime", "Indique uma hora de término válida.");
            }
        }

        var location = input.Location?.Trim();
        if (string.IsNullOrEmpty(location))
        {
            errors.Add("location", "O local é obrigatório.");
        }
        else if (location.Length > MaxLocationLength)
        {
            errors.Add("location", $"O local não pode exceder {MaxLocationLength} caracteres.");
        }

        var quorumBasis = existing?.QuorumBasis ?? QuorumBases[0];
        if (MeetingAccess.IsAssembly(meeting.Type))
        {
            if (input.QuorumBasis is not null && QuorumBases.Contains(input.QuorumBasis))
            {
                quorumBasis = input.QuorumBasis;
            }
            else
            {
                errors.Add("quorumBasis", "Escolha a base do quórum.");
            }
        }

        var firstSecretary = NullIfBlank(input.FirstSecretaryId);
        if (firstSecretary is not null && setup.FirstOptions.All(o => o.Id != firstSecretary))
        {
            errors.Add("firstSecretaryId", "Escolha um secretário da lista.");
        }

        var secondSecretary = NullIfBlank(input.SecondSecretaryId);
        if (secondSecretary is not null && setup.SecondOptions.All(o => o.Id != secondSecretary))
        {
            errors.Add("secondSecretaryId", "Escolha um secretário da lista.");
        }

        var closingText = NullIfBlank(input.ClosingText);
        if (closingText?.Length > MaxClosingTextLength)
        {
            errors.Add("closingText", $"O texto de encerramento não pode exceder {MaxClosingTextLength} caracteres.");
        }

        var points = ReadAgendaPoints(input.AgendaPoints, errors);

        if (errors.Any)
        {
            return Invalid<MeetingAtaEditorDto>(errors);
        }

        try
        {
            if (existing is null)
            {
                await _atas.CreateAtaAsync(new MeetingAta
                {
                    MeetingId = meeting.Id,
                    AtaNumber = ataNumber,
                    ActualStartTime = start,
                    ActualEndTime = end,
                    Location = location!,
                    PresidentUserId = setup.PresidentId,
                    FirstSecretaryUserId = firstSecretary,
                    SecondSecretaryUserId = secondSecretary,
                    QuorumBasis = quorumBasis,
                    AttendeesPresent = "[]",
                    AttendeesAbsent = "[]",
                    Status = MeetingAtaStatus.Draft,
                    ClosingText = closingText,
                    AgendaPoints = points,
                });
            }
            else
            {
                // Only the form's fields change; status, PDF data and the attendance JSON stay as stored.
                await _atas.UpdateAtaAsync(new MeetingAta
                {
                    Id = existing.Id,
                    MeetingId = meeting.Id,
                    AtaNumber = ataNumber,
                    ActualStartTime = start,
                    ActualEndTime = end,
                    Location = location!,
                    PresidentUserId = setup.PresidentId,
                    FirstSecretaryUserId = firstSecretary,
                    SecondSecretaryUserId = secondSecretary,
                    QuorumBasis = quorumBasis,
                    AttendeesPresent = existing.AttendeesPresent,
                    AttendeesAbsent = existing.AttendeesAbsent,
                    Status = existing.Status,
                    ClosingText = closingText,
                    GeneratedAt = existing.GeneratedAt,
                    PdfStorageUrl = existing.PdfStorageUrl,
                    UpdatedAt = existing.UpdatedAt,
                    UpdatedBy = existing.UpdatedBy,
                    AgendaPoints = points,
                });
            }
        }
        catch (InvalidOperationException)
        {
            // Created or published by someone else in the meantime.
            return EventResult<MeetingAtaEditorDto>.Fail(EventResultStatus.Closed);
        }

        return EventResult<MeetingAtaEditorDto>.Ok(await AtaEditorAsync(access, meeting, await _atas.GetByMeetingIdAsync(meeting.Id)));
    }

    public async Task<EventResult<MeetingAtaEditorDto>> PublishAtaAsync(int id, ClaimsPrincipal user)
    {
        var (access, refusal) = await AccessAsync(user);
        if (access is null)
        {
            return EventResult<MeetingAtaEditorDto>.Fail(refusal);
        }

        var (context, closed) = await AtaContextAsync(access, id);
        if (context is null)
        {
            return EventResult<MeetingAtaEditorDto>.Fail(closed);
        }

        if (!context.CanWrite)
        {
            return EventResult<MeetingAtaEditorDto>.Fail(EventResultStatus.Forbidden);
        }

        // Only a saved draft is published (the old "Publicar" appeared once the ata had been saved).
        if (context.Existing is null || context.Existing.Status == MeetingAtaStatus.Published)
        {
            return EventResult<MeetingAtaEditorDto>.Fail(EventResultStatus.Closed);
        }

        var meeting = context.Meeting;
        var details = await _atas.GetByIdWithDetailsAsync(context.Existing.Id);
        if (details is null)
        {
            return EventResult<MeetingAtaEditorDto>.Fail(EventResultStatus.NotFound);
        }

        details.Meeting ??= meeting;
        var pdf = _pdf.GenerateAtaPdf(details);

        // As before: the PDF goes to Documentation in the current fiscal year's folder; a storage failure is logged and
        // does not stop the publication.
        var fiscalYear = FiscalYearHelper.GetCurrentFiscalYearString();
        var folderName = meeting.Type == MeetingType.ConselhoVeteranos ? $"Atas CV {fiscalYear}"
            : MeetingAccess.IsAssembly(meeting.Type) ? $"Atas AG {fiscalYear}"
            : $"Atas {fiscalYear}";
        var folderPath = $"docs/{_environment.EnvironmentName}/{fiscalYear}/{folderName}/";
        try
        {
            await _documents.CreateFolderAsync(folderPath);
            using var stream = new MemoryStream(pdf);
            await _documents.UploadDocumentAsync(folderPath, AtaFileName(meeting), stream, "application/pdf");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create ATA folder or upload PDF to {FolderPath}, but ATA was published", folderPath);
        }

        try
        {
            await _atas.PublishAtaAsync(context.Existing.Id);
        }
        catch (InvalidOperationException)
        {
            return EventResult<MeetingAtaEditorDto>.Fail(EventResultStatus.Closed);
        }

        return EventResult<MeetingAtaEditorDto>.Ok(await AtaEditorAsync(access, meeting, await _atas.GetByMeetingIdAsync(meeting.Id)));
    }

    public async Task<EventResult<MeetingAtaPdf>> GetAtaPdfAsync(int id, ClaimsPrincipal user)
    {
        var (access, refusal) = await AccessAsync(user);
        if (access is null)
        {
            return EventResult<MeetingAtaPdf>.Fail(refusal);
        }

        var (context, closed) = await AtaContextAsync(access, id);
        if (context is null)
        {
            return EventResult<MeetingAtaPdf>.Fail(closed);
        }

        // Writers download their draft from the editor; everyone else only a published ata they may read (A1).
        var ata = context.CanWrite ? context.Existing : ReadableAta(access, context);
        if (ata is null)
        {
            return EventResult<MeetingAtaPdf>.Fail(EventResultStatus.NotFound);
        }

        var details = await _atas.GetByIdWithDetailsAsync(ata.Id);
        if (details is null)
        {
            return EventResult<MeetingAtaPdf>.Fail(EventResultStatus.NotFound);
        }

        details.Meeting ??= context.Meeting;
        return EventResult<MeetingAtaPdf>.Ok(new MeetingAtaPdf(_pdf.GenerateAtaPdf(details), AtaFileName(details.Meeting)));
    }

    public async Task<EventResult<MeetingAtaViewDto>> ConfirmAtaAsync(int id, MeetingAtaConfirmationInput input, ClaimsPrincipal user)
    {
        var (access, refusal) = await AccessAsync(user);
        if (access is null)
        {
            return EventResult<MeetingAtaViewDto>.Fail(refusal);
        }

        var (context, closed) = await AtaContextAsync(access, id);
        if (context is null)
        {
            return EventResult<MeetingAtaViewDto>.Fail(closed);
        }

        var ata = ReadableAta(access, context);
        if (ata is null)
        {
            return EventResult<MeetingAtaViewDto>.Fail(EventResultStatus.NotFound);
        }

        if (ata.Status != MeetingAtaStatus.Published)
        {
            return EventResult<MeetingAtaViewDto>.Fail(EventResultStatus.Closed);
        }

        // Only someone who attended ("Vou") confirms or refuses, as the old view and the confirmation service required.
        var participation = await _participations.GetParticipationByMeetingAndUserAsync(context.Meeting.Id, access.UserId);
        if (participation?.WillAttend != true)
        {
            return EventResult<MeetingAtaViewDto>.Fail(EventResultStatus.Forbidden);
        }

        if (input.Confirm)
        {
            await _confirmations.ConfirmAtaAsync(ata.Id, access.UserId);
        }
        else
        {
            await _confirmations.RefuseAtaAsync(ata.Id, access.UserId);
        }

        return EventResult<MeetingAtaViewDto>.Ok(await AtaViewAsync(access, ata.Id));
    }

    // ---------- meeting requests ----------

    public async Task<EventResult<MeetingRequestPageDto>> GetRequestsAsync(
        string? status, string? fiscalYear, int? page, int? pageSize, ClaimsPrincipal user)
    {
        var (access, refusal) = await AccessAsync(user);
        if (access is null)
        {
            return EventResult<MeetingRequestPageDto>.Fail(refusal);
        }

        if (!access.CanSeeRequests)
        {
            return EventResult<MeetingRequestPageDto>.Fail(EventResultStatus.Forbidden);
        }

        RequestStatus? filter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<RequestStatus>(status, ignoreCase: false, out var parsed) || !RequestFilters.Contains(parsed) || int.TryParse(status, out _))
            {
                return EventResult<MeetingRequestPageDto>.Invalid("status", "Escolha um estado da lista.");
            }

            filter = parsed;
        }

        if (!TryFiscalYear(fiscalYear, out _, out var range))
        {
            return EventResult<MeetingRequestPageDto>.Invalid("fy", "Escolha um ano da lista.");
        }

        var size = pageSize is { } requested && RequestPageSizes.Contains(requested) ? requested : RequestPageSizes[0];
        var number = page is > 0 ? page.Value : 1;

        IEnumerable<MeetingRequest> requests = await _requests.GetPagedAsync(1, int.MaxValue, filter);
        if (range is { } r)
        {
            requests = requests.Where(x => x.ProposedDateTime >= r.Start && x.ProposedDateTime <= r.End);
        }

        var all = requests.OrderByDescending(x => x.ProposedDateTime).ToList();
        var items = all.Skip((number - 1) * size).Take(size).Select(x => RequestDto(access, x)).ToList();
        return EventResult<MeetingRequestPageDto>.Ok(new MeetingRequestPageDto(items, all.Count, number, size));
    }

    public async Task<EventResult<int>> ProposeAsync(MeetingRequestInput input, ClaimsPrincipal user)
    {
        var (access, refusal) = await AccessAsync(user);
        if (access is null)
        {
            return EventResult<int>.Fail(refusal);
        }

        if (!TryType(input.Type, out var type))
        {
            return EventResult<int>.Invalid("type", "Escolha o tipo de reunião.");
        }

        // The page proposed CV, Direção and (as "Assembleia Geral") AGE; nothing proposed an AGO.
        var allowed = type switch
        {
            MeetingType.ConselhoVeteranos => access.CanProposeCv,
            MeetingType.ReuniaoDirecao => access.CanProposeDirecao,
            MeetingType.AssembleiaGeralExtraordinaria => access.CanProposeAg,
            _ => false,
        };
        if (!allowed)
        {
            return EventResult<int>.Fail(EventResultStatus.Forbidden);
        }

        var errors = new Errors();
        var title = RequiredText(input.Title, "title", "O título é obrigatório.", MaxTitleLength, "O título não pode exceder 200 caracteres.", errors);
        if (!TryDate(input.ProposedDate, out var proposed))
        {
            errors.Add("proposedDate", "A data proposta é obrigatória.");
        }

        var location = OptionalText(input.Location, "location", MaxLocationLength, "A localização não pode exceder 200 caracteres.", errors);
        var description = input.Description ?? string.Empty;
        if (string.IsNullOrWhiteSpace(description))
        {
            errors.Add("description", "A descrição é obrigatória.");
        }
        else if (description.Length > MaxDescriptionLength)
        {
            errors.Add("description", $"A descrição não pode exceder {MaxDescriptionLength} caracteres.");
        }

        if (errors.Any)
        {
            return Invalid<int>(errors);
        }

        // The old service push to Owners and the relevant president comes with it.
        var created = await _requests.CreateAsync(new MeetingRequest
        {
            RequestedMeetingType = type,
            Title = title!,
            ProposedDateTime = proposed,
            Location = location,
            Description = description,
            AuthorUserId = access.UserId,
            Status = RequestStatus.Pending,
        });
        return EventResult<int>.Ok(created.Id);
    }

    public async Task<EventResult<MeetingDraftDto>> AcceptRequestAsync(int requestId, ClaimsPrincipal user)
    {
        var (request, refusal) = await DecidableRequestAsync(requestId, user);
        if (request is null)
        {
            return EventResult<MeetingDraftDto>.Fail(refusal);
        }

        await _requests.UpdateStatusAsync(request.Id, RequestStatus.Confirmed);

        // The create form then opens prefilled with the request, as before.
        return EventResult<MeetingDraftDto>.Ok(new MeetingDraftDto(
            request.RequestedMeetingType.ToString(),
            request.Title,
            Format(request.ProposedDateTime),
            request.Location,
            request.Description));
    }

    public async Task<EventResult<bool>> RejectRequestAsync(int requestId, ClaimsPrincipal user)
    {
        var (request, refusal) = await DecidableRequestAsync(requestId, user);
        if (request is null)
        {
            return EventResult<bool>.Fail(refusal);
        }

        // The old service pushes the rejection to the author.
        await _requests.UpdateStatusAsync(request.Id, RequestStatus.Rejected);
        return EventResult<bool>.Ok(true);
    }

    public async Task<EventResult<bool>> DeleteRequestAsync(int requestId, ClaimsPrincipal user)
    {
        var (request, refusal) = await OwnRequestAsync(requestId, user);
        if (request is null)
        {
            return EventResult<bool>.Fail(refusal);
        }

        await _requests.DeleteAsync(request.Id);
        return EventResult<bool>.Ok(true);
    }

    public async Task<EventResult<bool>> RemindRequestAsync(int requestId, ClaimsPrincipal user, string baseUrl)
    {
        var (request, refusal) = await OwnRequestAsync(requestId, user);
        if (request is null)
        {
            return EventResult<bool>.Fail(refusal);
        }

        if (request.Status != RequestStatus.Pending)
        {
            return EventResult<bool>.Fail(EventResultStatus.Closed);
        }

        return await _requests.SendReminderNotificationAsync(request.Id, baseUrl)
            ? EventResult<bool>.Ok(true)
            : EventResult<bool>.Invalid("notice", "Não foi possível enviar o lembrete.");
    }

    // ---------- access ----------

    /// <summary>The session's member, or why not: a visitor must sign in, a Leitão is refused everything.</summary>
    private async Task<(MeetingAccess? Access, EventResultStatus Refusal)> AccessAsync(ClaimsPrincipal principal)
    {
        var userId = principal.Identity?.IsAuthenticated == true ? principal.FindFirstValue(ClaimTypes.NameIdentifier) : null;
        if (string.IsNullOrEmpty(userId))
        {
            return (null, EventResultStatus.SignInRequired);
        }

        await using var db = await _contexts.CreateDbContextAsync();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
        {
            return (null, EventResultStatus.SignInRequired);
        }

        var access = new MeetingAccess(user, principal.IsInRole("Owner"), principal.IsInRole("Admin"));
        return access.IsBlocked ? (null, EventResultStatus.Forbidden) : (access, EventResultStatus.Ok);
    }

    /// <summary>The meetings this member sees: the old service filter (CV, Direção, Leitão), unchanged.</summary>
    private async Task<List<Meeting>> VisibleMeetingsAsync(MeetingAccess access) =>
        (await _meetings.GetAllMeetingsAsync(null, 1, int.MaxValue, access.UserId)).ToList();

    private async Task<Meeting?> VisibleMeetingAsync(MeetingAccess access, int id) =>
        (await VisibleMeetingsAsync(access)).FirstOrDefault(m => m.Id == id);

    /// <summary>A meeting whose email, push or cancel the member may send: theirs to manage, upcoming, not cancelled.</summary>
    private async Task<(Meeting? Meeting, EventResultStatus Refusal)> NotifiableAsync(MeetingAccess access, int id)
    {
        var meeting = await VisibleMeetingAsync(access, id);
        if (meeting is null)
        {
            return (null, EventResultStatus.NotFound);
        }

        if (!access.CanManage(meeting))
        {
            return (null, EventResultStatus.Forbidden);
        }

        return meeting.IsCancelled || meeting.Date.Date < DateTime.Today
            ? (null, EventResultStatus.Closed)
            : (meeting, EventResultStatus.Ok);
    }

    /// <summary>A meeting whose participants list may be opened: seen by the member and not cancelled.</summary>
    private async Task<(Meeting? Meeting, EventResultStatus Refusal)> ParticipantsMeetingAsync(MeetingAccess access, int id)
    {
        var meeting = await VisibleMeetingAsync(access, id);
        if (meeting is null)
        {
            return (null, EventResultStatus.NotFound);
        }

        return meeting.IsCancelled ? (null, EventResultStatus.Closed) : (meeting, EventResultStatus.Ok);
    }

    private sealed record AtaContext(Meeting Meeting, MeetingAta? Existing, bool CanWrite);

    /// <summary>
    /// A meeting whose ata may be touched: seen, not cancelled, on or after its day. <c>CanWrite</c> is the old
    /// <c>CanCreateOrEditAta</c> with the old page's knowledge: it only knew a meeting's ata from the day after, so on the
    /// day itself the secretaries named on an ata are not yet counted.
    /// </summary>
    private async Task<(AtaContext? Context, EventResultStatus Refusal)> AtaContextAsync(MeetingAccess access, int id)
    {
        var meeting = await VisibleMeetingAsync(access, id);
        if (meeting is null)
        {
            return (null, EventResultStatus.NotFound);
        }

        if (meeting.IsCancelled || meeting.Date.Date > DateTime.Today)
        {
            return (null, EventResultStatus.Closed);
        }

        var existing = await _atas.GetByMeetingIdAsync(meeting.Id);
        return (new AtaContext(meeting, existing, CanWriteAta(access, meeting, existing)), EventResultStatus.Ok);
    }

    /// <summary>
    /// The ata "Ver Ata" may open: the meeting is before today, the member passes the old reading rule, and the ata is
    /// published, or a draft they may write (decision A1). Null = nothing to show (a draft is never revealed).
    /// </summary>
    private static MeetingAta? ReadableAta(MeetingAccess access, AtaContext context)
    {
        var ata = context.Existing;
        if (ata is null || context.Meeting.Date.Date >= DateTime.Today || !access.CanReadPublishedAta(context.Meeting))
        {
            return null;
        }

        return ata.Status == MeetingAtaStatus.Published || context.CanWrite ? ata : null;
    }

    /// <summary>The old ata-writing rule; roles are "Owner" or nothing (decision A2), an existing ata only from the day after.</summary>
    private bool CanWriteAta(MeetingAccess access, Meeting meeting, MeetingAta? existing) =>
        _atas.CanCreateOrEditAta(
            access.UserId,
            meeting,
            access.AtaRoles,
            access.User.Positions ?? new List<Position>(),
            meeting.Date.Date < DateTime.Today ? existing : null);

    private async Task<(MeetingRequest? Request, EventResultStatus Refusal)> DecidableRequestAsync(int requestId, ClaimsPrincipal user)
    {
        var (access, refusal) = await AccessAsync(user);
        if (access is null)
        {
            return (null, refusal);
        }

        if (!access.CanSeeRequests)
        {
            return (null, EventResultStatus.Forbidden);
        }

        var request = await _requests.GetByIdAsync(requestId);
        if (request is null)
        {
            return (null, EventResultStatus.NotFound);
        }

        if (!access.CanDecide(request))
        {
            return (null, EventResultStatus.Forbidden);
        }

        return request.Status == RequestStatus.Pending ? (request, EventResultStatus.Ok) : (null, EventResultStatus.Closed);
    }

    private async Task<(MeetingRequest? Request, EventResultStatus Refusal)> OwnRequestAsync(int requestId, ClaimsPrincipal user)
    {
        var (access, refusal) = await AccessAsync(user);
        if (access is null)
        {
            return (null, refusal);
        }

        if (!access.CanSeeRequests)
        {
            return (null, EventResultStatus.Forbidden);
        }

        var request = await _requests.GetByIdAsync(requestId);
        if (request is null)
        {
            return (null, EventResultStatus.NotFound);
        }

        return access.OwnsRequest(request) ? (request, EventResultStatus.Ok) : (null, EventResultStatus.Forbidden);
    }

    // ---------- cards ----------

    private async Task<MeetingCardDto> CardAsync(MeetingAccess access, Meeting meeting) =>
        (await CardsAsync(access, new[] { meeting }))[0];

    private async Task<MeetingCardDto?> CardOrNullAsync(MeetingAccess access, int id) =>
        await VisibleMeetingAsync(access, id) is { } meeting ? await CardAsync(access, meeting) : null;

    private async Task<List<MeetingCardDto>> CardsAsync(MeetingAccess access, IReadOnlyList<Meeting> meetings)
    {
        var today = DateTime.Today;
        var ids = meetings.Select(m => m.Id).ToList();
        var counts = ids.Count == 0 ? new Dictionary<int, int>() : await _participations.GetParticipationCountsByMeetingIdsAsync(ids);
        var mine = (await _participations.GetParticipationsByUserIdAsync(access.UserId))
            .Where(p => p.UserId == access.UserId)
            .GroupBy(p => p.MeetingId)
            .ToDictionary(g => g.Key, g => g.First());

        // The old page looked atas up for its "past" list only (before today).
        var pastIds = meetings.Where(m => m.Date.Date < today).Select(m => m.Id).ToList();
        var atas = pastIds.Count == 0 ? new Dictionary<int, MeetingAta>() : await _atas.GetByMeetingIdsAsync(pastIds);

        return meetings
            .Select(m => Card(access, m, counts.GetValueOrDefault(m.Id), mine.GetValueOrDefault(m.Id), atas.GetValueOrDefault(m.Id), today))
            .ToList();
    }

    private MeetingCardDto Card(MeetingAccess access, Meeting m, int going, MeetingParticipation? mine, MeetingAta? ata, DateTime today)
    {
        var past = m.Date.Date < today;
        var completed = !m.IsCancelled && m.Date.Date <= today;
        var knownAta = past ? ata : null;
        var canWrite = completed && CanWriteAta(access, m, knownAta);
        var published = knownAta?.Status == MeetingAtaStatus.Published;
        var readable = past && !m.IsCancelled && knownAta is not null && access.CanReadPublishedAta(m) && (published || canWrite);
        var manage = access.CanManage(m);

        return new MeetingCardDto(
            m.Id,
            m.Type.ToString(),
            TypeLabel(m.Type),
            m.Title,
            Format(m.Date),
            m.Location,
            m.Statement ?? string.Empty,
            m.Organizer?.Nickname,
            m.Organizer?.Positions is { Count: > 0 } positions ? PositionHelper.GetDisplayName(positions[0]) : null,
            m.Type == MeetingType.ConselhoVeteranos ? m.TunoRepresentative?.Nickname : null,
            manage ? m.TunoRepresentativeUserId : null,
            m.IsCancelled,
            completed,
            past,
            going,
            mine is null ? null : new MeetingMyParticipationDto(mine.Id, mine.WillAttend, mine.Notes),
            readable ? (published ? "published" : "draft") : null,
            new MeetingCardAccessDto(
                Manage: manage,
                Notify: manage && !m.IsCancelled && m.Date.Date >= today,
                Uncancel: manage && m.IsCancelled,
                Respond: !m.IsCancelled && !completed,
                RemoveOwn: completed && mine is not null,
                Participants: !m.IsCancelled,
                WriteAta: canWrite && !published,
                ViewAta: readable));
    }

    private async Task<MeetingParticipantsDto> ParticipantsAsync(MeetingAccess access, Meeting meeting)
    {
        var participations = (await _participations.GetParticipationsByMeetingIdAsync(meeting.Id)).ToList();
        MeetingParticipantDto Participant(MeetingParticipation p) => new(
            p.Id,
            p.User?.Nickname ?? string.Empty,
            $"{p.User?.FirstName} {p.User?.LastName}".Trim(),
            p.User?.ProfilePictureSrc ?? "/images/default-avatar.webp",
            p.Notes,
            Badge(p.User),
            access.CanRemoveParticipation(p));

        return new MeetingParticipantsDto(
            meeting.Id,
            meeting.Title,
            participations.Where(p => p.WillAttend).Select(Participant).ToList(),
            participations.Where(p => !p.WillAttend).Select(Participant).ToList(),
            access.CanAddParticipants);
    }

    private static string? Badge(ApplicationUser? user)
    {
        if (user is null)
        {
            return null;
        }

        if (user.Positions?.Contains(Position.Magister) == true)
        {
            return "Magister";
        }

        return user.Categories?.Contains(MemberCategory.Tuno) == true ? "Tuno"
            : user.Categories?.Contains(MemberCategory.Caloiro) == true ? "Caloiro"
            : null;
    }

    private MeetingRequestDto RequestDto(MeetingAccess access, MeetingRequest r)
    {
        var pending = r.Status == RequestStatus.Pending;
        return new MeetingRequestDto(
            r.Id,
            r.RequestedMeetingType.ToString(),
            TypeLabel(r.RequestedMeetingType),
            r.Title,
            r.Author?.Nickname ?? "Desconhecido",
            Format(r.ProposedDateTime),
            r.Location,
            r.Description,
            r.Status.ToString(),
            access.OwnsRequest(r),
            access.OwnsRequest(r) && pending,
            access.CanDecide(r) && pending);
    }

    // ---------- atas: view and editor ----------

    private async Task<MeetingAtaViewDto> AtaViewAsync(MeetingAccess access, int ataId)
    {
        var ata = await _atas.GetByIdWithDetailsAsync(ataId) ?? throw new InvalidOperationException($"Ata {ataId} not found.");
        var meeting = ata.Meeting;
        var published = ata.Status == MeetingAtaStatus.Published;
        var present = meeting.Participations.Where(p => p.WillAttend && p.User != null).ToList();

        IReadOnlyList<string> confirmed = Array.Empty<string>();
        MeetingAtaMyConfirmationDto? mine = null;
        if (published)
        {
            confirmed = (await _confirmations.GetConfirmationsByAtaIdAsync(ata.Id))
                .Where(c => c.IsConfirmed == true && c.User != null)
                .Select(c => FullName(c.User))
                .ToList();

            if (present.Any(p => p.UserId == access.UserId))
            {
                var own = await _confirmations.GetUserConfirmationAsync(ata.Id, access.UserId);
                mine = new MeetingAtaMyConfirmationDto(own?.IsConfirmed, own?.Notes);
            }
        }

        var isCouncil = meeting.Type == MeetingType.ConselhoVeteranos;
        return new MeetingAtaViewDto(
            meeting.Id,
            published ? "published" : "draft",
            Kind(meeting.Type),
            TypeLabel(meeting.Type),
            ata.AtaNumber,
            Format(meeting.Date),
            ata.Location,
            ata.PresidentUser is null ? null : FullName(ata.PresidentUser),
            ata.FirstSecretaryUser is null ? null : FullName(ata.FirstSecretaryUser),
            isCouncil || ata.SecondSecretaryUser is null ? null : FullName(ata.SecondSecretaryUser),
            present.Select(p => FullName(p.User!)).ToList(),
            confirmed,
            mine,
            ata.AgendaPoints.OrderBy(p => p.PointNumber).Select(PointDto).ToList(),
            ata.ClosingText);
    }

    private sealed record AtaSetup(
        string PresidentId,
        string? PresidentName,
        string? DefaultFirstSecretaryId,
        string? DefaultSecondSecretaryId,
        IReadOnlyList<MeetingPersonOptionDto> FirstOptions,
        IReadOnlyList<MeetingPersonOptionDto> SecondOptions,
        IReadOnlyList<string> Present);

    /// <summary>
    /// The old ata form's people: the president is the current holder of the position (CV: Presidente do CV; AG:
    /// Presidente da Mesa), else whoever is saved (a new ata: the writer). CV secretaries come from those who said
    /// "Vou"; AG secretaries are the Mesa's current holders. A secretary already saved stays selectable.
    /// </summary>
    private async Task<AtaSetup> AtaSetupAsync(MeetingAccess access, Meeting meeting, MeetingAta? ata)
    {
        var users = await UsersAsync();
        var participants = (await _participations.GetParticipationsByMeetingIdAsync(meeting.Id))
            .Where(p => p.WillAttend && p.User != null)
            .ToList();
        var isAssembly = MeetingAccess.IsAssembly(meeting.Type);
        var isCouncil = meeting.Type == MeetingType.ConselhoVeteranos;

        var holder = isCouncil ? Holder(users, Position.PresidenteConselhoVeteranos)
            : isAssembly ? Holder(users, Position.PresidenteMesaAssembleia)
            : null;
        var firstHolder = isAssembly ? Holder(users, Position.PrimeiroSecretarioMesaAssembleia) : null;
        var secondHolder = isAssembly ? Holder(users, Position.SegundoSecretarioMesaAssembleia) : null;
        var presidentId = holder?.Id ?? ata?.PresidentUserId ?? access.UserId;

        var firstOptions = new List<MeetingPersonOptionDto>();
        var secondOptions = new List<MeetingPersonOptionDto>();
        if (isCouncil)
        {
            firstOptions.AddRange(participants.Select(p => new MeetingPersonOptionDto(p.UserId, p.User!.Nickname ?? p.User.FirstName ?? string.Empty)));
        }
        else if (isAssembly)
        {
            if (firstHolder is not null)
            {
                firstOptions.Add(new MeetingPersonOptionDto(firstHolder.Id, $"{firstHolder.Nickname ?? firstHolder.FirstName} (Cargo atual)"));
            }

            if (secondHolder is not null)
            {
                secondOptions.Add(new MeetingPersonOptionDto(secondHolder.Id, $"{secondHolder.Nickname ?? secondHolder.FirstName} (Cargo atual)"));
            }
        }

        KeepSaved(firstOptions, ata?.FirstSecretaryUserId, users);
        KeepSaved(secondOptions, ata?.SecondSecretaryUserId, users);

        return new AtaSetup(
            presidentId,
            users.FirstOrDefault(u => u.Id == presidentId) is { } president ? president.Nickname ?? president.FirstName : null,
            ata is null ? (isAssembly ? firstHolder?.Id : null) : ata.FirstSecretaryUserId,
            ata is null ? (isAssembly ? secondHolder?.Id : null) : ata.SecondSecretaryUserId,
            firstOptions,
            secondOptions,
            participants.Select(p => FullName(p.User!)).ToList());
    }

    private static void KeepSaved(List<MeetingPersonOptionDto> options, string? savedId, List<ApplicationUser> users)
    {
        if (string.IsNullOrEmpty(savedId) || options.Any(o => o.Id == savedId))
        {
            return;
        }

        var saved = users.FirstOrDefault(u => u.Id == savedId);
        options.Insert(0, new MeetingPersonOptionDto(savedId, saved is null ? "(membro removido)" : saved.Nickname ?? saved.FirstName ?? string.Empty));
    }

    private async Task<MeetingAtaEditorDto> AtaEditorAsync(MeetingAccess access, Meeting meeting, MeetingAta? stored)
    {
        var ata = stored is null ? null : await _atas.GetByIdWithDetailsAsync(stored.Id);
        var setup = await AtaSetupAsync(access, meeting, ata);
        return new MeetingAtaEditorDto(
            ata?.Id,
            ata?.Status == MeetingAtaStatus.Published ? "published" : "draft",
            Kind(meeting.Type),
            TypeLabel(meeting.Type),
            meeting.Title,
            Format(meeting.Date),
            ata?.AtaNumber,
            Format(ata?.ActualStartTime ?? meeting.Date),
            ata?.ActualEndTime is { } end ? Format(end) : null,
            ata?.Location ?? meeting.Location ?? string.Empty,
            ata?.QuorumBasis ?? QuorumBases[0],
            setup.PresidentName,
            setup.DefaultFirstSecretaryId,
            setup.DefaultSecondSecretaryId,
            setup.FirstOptions,
            setup.SecondOptions,
            setup.Present,
            ata?.AgendaPoints.OrderBy(p => p.PointNumber).Select(PointDto).ToList() ?? new List<MeetingAgendaPointDto>(),
            ata?.ClosingText,
            ata?.GeneratedAt is { } generated ? Format(generated) : null);
    }

    private static List<MeetingAtaAgendaPoint> ReadAgendaPoints(IReadOnlyList<MeetingAgendaPointInput>? input, Errors errors)
    {
        var points = new List<MeetingAtaAgendaPoint>();
        if (input is null)
        {
            return points;
        }

        if (input.Count > MaxAgendaPoints)
        {
            errors.Add("agendaPoints", $"No máximo {MaxAgendaPoints} pontos.");
            return points;
        }

        for (var i = 0; i < input.Count; i++)
        {
            var point = input[i];
            var key = $"agendaPoints[{i}]";
            var title = point.Title?.Trim();
            if (string.IsNullOrEmpty(title))
            {
                errors.Add(key, "Cada ponto precisa de um título.");
                continue;
            }

            if (title.Length > MaxAgendaTitleLength
                || point.Discussion?.Length > MaxAgendaTextLength
                || point.Decision?.Length > MaxAgendaTextLength)
            {
                errors.Add(key, $"Título até {MaxAgendaTitleLength} caracteres; discussão e deliberação até {MaxAgendaTextLength}.");
                continue;
            }

            if (!ValidVotes(point.VotesFor) || !ValidVotes(point.VotesAgainst) || !ValidVotes(point.VotesAbstain))
            {
                errors.Add(key, "Os votos têm de ser números entre 0 e 100000.");
                continue;
            }

            var result = NullIfBlank(point.Result);
            if (result is not null && !VoteResults.Contains(result))
            {
                errors.Add(key, "O resultado é Aprovado ou Rejeitado.");
                continue;
            }

            points.Add(new MeetingAtaAgendaPoint
            {
                PointNumber = i + 1,
                Title = title,
                DiscussionSummary = NullIfBlank(point.Discussion),
                DecisionText = NullIfBlank(point.Decision),
                VotesFor = point.VotesFor,
                VotesAgainst = point.VotesAgainst,
                VotesAbstain = point.VotesAbstain,
                VoteResult = result,
            });
        }

        return points;
    }

    private static bool ValidVotes(int? votes) => votes is null or (>= 0 and <= MaxVotes);

    private static MeetingAgendaPointDto PointDto(MeetingAtaAgendaPoint p) =>
        new(p.PointNumber, p.Title, p.DiscussionSummary, p.DecisionText, p.VotesFor, p.VotesAgainst, p.VotesAbstain, p.VoteResult);

    private static string AtaFileName(Meeting meeting) => $"Ata_{meeting.Type}_{meeting.Date:yyyyMMdd}.pdf";

    // ---------- the meeting form ----------

    /// <param name="savedRepresentativeId">The representative already saved on the meeting being edited: it stays valid even
    /// once that member is no longer a plain Tuno (the old form kept it selected).</param>
    private (Meeting? Meeting, Errors Errors) ReadMeeting(
        MeetingInput input, MeetingAccess access, List<ApplicationUser> users, string? savedRepresentativeId)
    {
        var errors = new Errors();
        if (!TryType(input.Type, out var type) || !access.FormTypes.Contains(type))
        {
            errors.Add("type", "Escolha um tipo de reunião da lista.");
        }

        var title = RequiredText(input.Title, "title", "O título é obrigatório.", MaxTitleLength, "O título não pode exceder 200 caracteres.", errors);
        if (!TryDate(input.Date, out var date))
        {
            errors.Add("date", "A data é obrigatória.");
        }

        var location = OptionalText(input.Location, "location", MaxLocationLength, "A localização não pode exceder 200 caracteres.", errors);
        var statement = input.Statement ?? string.Empty;
        if (string.IsNullOrWhiteSpace(statement))
        {
            errors.Add("statement", "A declaração é obrigatória.");
        }
        else if (statement.Length > MaxStatementLength)
        {
            errors.Add("statement", $"A declaração não pode exceder {MaxStatementLength} caracteres.");
        }

        // A Tuno representative only for a CV, and only one the form offered (or the one already saved).
        string? representative = null;
        if (type == MeetingType.ConselhoVeteranos && !string.IsNullOrWhiteSpace(input.TunoRepresentativeId))
        {
            if (input.TunoRepresentativeId == savedRepresentativeId
                || TunoRepresentativeCandidates(users).Any(u => u.Id == input.TunoRepresentativeId))
            {
                representative = input.TunoRepresentativeId;
            }
            else
            {
                errors.Add("tunoRepresentativeId", "Escolha um Tuno da lista.");
            }
        }

        if (errors.Any)
        {
            return (null, errors);
        }

        return (new Meeting
        {
            Type = type,
            Title = title!,
            Date = date,
            Location = location,
            Statement = statement,
            TunoRepresentativeUserId = representative,
        }, errors);
    }

    // ---------- audiences ----------

    private sealed record Audience(List<ApplicationUser> Receiving, List<ApplicationUser> NotReceiving);

    private static bool InCouncilAudience(ApplicationUser u) =>
        u.CurrentRole is MeetingAccess.Veterano or MeetingAccess.Tunossauro || u.Positions?.Contains(Position.Magister) == true;

    private static bool InDirecaoAudience(ApplicationUser u, HashSet<string> admins) =>
        MeetingAccess.HoldsDirecaoPosition(u) || (admins.Contains(u.Id) && u.IsTuno());

    private static IEnumerable<ApplicationUser> ByName(IEnumerable<ApplicationUser> users) => users.OrderBy(u => u.Nickname ?? u.FirstName);

    /// <summary>
    /// The old email / cancellation audiences. CV: Veterano / Tunossauro / Magister with email on (not receiving: the plain
    /// Tunos). Direção: Direção positions and Admins with the Tuno category. AG: everyone but Leitões with email on (not
    /// receiving: confirmed addresses with email off; the cancel form also leaves Leitões out of that list).
    /// </summary>
    private async Task<Audience> EmailAudienceAsync(Meeting meeting, List<ApplicationUser> users, bool forCancellation)
    {
        switch (meeting.Type)
        {
            case MeetingType.ConselhoVeteranos:
                return new Audience(
                    ByName(users.Where(u => u.Subscribed && u.EmailConfirmed && InCouncilAudience(u))).ToList(),
                    ByName(users.Where(u => u.EmailConfirmed && u.IsTuno() && u.CurrentRole == MeetingAccess.Tuno
                                            && u.Positions?.Contains(Position.Magister) != true)).ToList());

            case MeetingType.ReuniaoDirecao:
                var admins = await AdminIdsAsync();
                return new Audience(
                    ByName(users.Where(u => u.Subscribed && u.EmailConfirmed && InDirecaoAudience(u, admins))).ToList(),
                    ByName(users.Where(u => u.EmailConfirmed && !u.Subscribed && InDirecaoAudience(u, admins))).ToList());

            default:
                return new Audience(
                    ByName(users.Where(u => u.Subscribed && u.EmailConfirmed && !u.IsLeitao())).ToList(),
                    ByName(users.Where(u => u.EmailConfirmed && !u.Subscribed && (!forCancellation || !u.IsLeitao()))).ToList());
        }
    }

    /// <summary>The old push audience: the meeting type's members, split by an active push subscription.</summary>
    private async Task<Audience> PushAudienceAsync(Meeting meeting, List<ApplicationUser> users)
    {
        var subscribed = (await _push.GetSubscribedUserIdsAsync()).ToHashSet();
        IEnumerable<ApplicationUser> eligible = meeting.Type switch
        {
            MeetingType.ConselhoVeteranos => users.Where(InCouncilAudience),
            MeetingType.ReuniaoDirecao => await DirecaoAsync(users),
            _ => users.Where(u => !u.IsLeitao()),
        };

        var ordered = ByName(eligible).ToList();
        return new Audience(ordered.Where(u => subscribed.Contains(u.Id)).ToList(), ordered.Where(u => !subscribed.Contains(u.Id)).ToList());
    }

    private async Task<IEnumerable<ApplicationUser>> DirecaoAsync(List<ApplicationUser> users)
    {
        var admins = await AdminIdsAsync();
        return users.Where(u => InDirecaoAudience(u, admins));
    }

    /// <summary>The Tunos a CV email may add (confirmed address, plain Tuno by time).</summary>
    private static List<ApplicationUser> EmailTunoOptions(Meeting meeting, List<ApplicationUser> users) =>
        meeting.Type == MeetingType.ConselhoVeteranos
            ? ByName(users.Where(u => u.EmailConfirmed && u.CurrentRole == MeetingAccess.Tuno)).ToList()
            : new List<ApplicationUser>();

    /// <summary>The Tunos a CV push may add (plain Tuno by time).</summary>
    private static List<ApplicationUser> PushTunoOptions(Meeting meeting, List<ApplicationUser> users) =>
        meeting.Type == MeetingType.ConselhoVeteranos
            ? ByName(users.Where(u => u.CurrentRole == MeetingAccess.Tuno)).ToList()
            : new List<ApplicationUser>();

    /// <summary>The Tuno representative choices: plain Tunos by time, no Magister.</summary>
    private static IEnumerable<ApplicationUser> TunoRepresentativeCandidates(List<ApplicationUser> users) =>
        ByName(users.Where(u => u.CurrentRole == MeetingAccess.Tuno && u.Positions?.Contains(Position.Magister) != true));

    private async Task<HashSet<string>> AdminIdsAsync()
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var ids = await (from link in db.UserRoles
                         join role in db.Roles on link.RoleId equals role.Id
                         where role.Name == "Admin"
                         select link.UserId).ToListAsync();
        return ids.ToHashSet();
    }

    // ponytail: whole Users table in memory, as the old page did (~100 rows); filter in SQL if it grows.
    private async Task<List<ApplicationUser>> UsersAsync()
    {
        await using var db = await _contexts.CreateDbContextAsync();
        return await db.Users.AsNoTracking().ToListAsync();
    }

    private static ApplicationUser? Holder(List<ApplicationUser> users, Position position) =>
        users.FirstOrDefault(u => u.Positions != null && u.Positions.Contains(position));

    // ---------- email helpers ----------

    private static (List<string> Emails, Dictionary<string, (string nickname, string fullName)> Data) Addresses(IEnumerable<ApplicationUser> people)
    {
        var withAddress = people
            .Where(u => !string.IsNullOrWhiteSpace(u.Email))
            .DistinctBy(u => u.Email!, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return (
            withAddress.Select(u => u.Email!).ToList(),
            withAddress.ToDictionary(u => u.Email!, u => (u.Nickname ?? string.Empty, $"{u.FirstName ?? string.Empty} {u.LastName ?? string.Empty}".Trim())));
    }

    /// <summary>Runs one email send; a thrown error or a refusal comes back as text for the dialog.</summary>
    private async Task<(int Count, string? Warning, string? Error)> SendEmailsAsync(
        Func<Task<(bool success, int count, string? errorMessage)>> send, int meetingId)
    {
        try
        {
            var (success, count, error) = await send();
            return success ? (count, error, null) : (0, null, error ?? "Não foi possível enviar os emails.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send meeting emails for meeting {MeetingId}", meetingId);
            return (0, null, "Não foi possível enviar os emails.");
        }
    }

    /// <summary>The old sender: for a CV the Presidente do CV (when there is one), otherwise whoever sends, with their first position.</summary>
    private static (string Name, string? Position) Sender(Meeting meeting, ApplicationUser me, IEnumerable<ApplicationUser> users)
    {
        if (meeting.Type == MeetingType.ConselhoVeteranos
            && users.FirstOrDefault(u => u.Positions != null && u.Positions.Contains(Position.PresidenteConselhoVeteranos)) is { } president)
        {
            return ($"{president.FirstName} \"{president.Nickname}\" {president.LastName}",
                PositionHelper.GetDisplayName(Position.PresidenteConselhoVeteranos));
        }

        return ($"{me.FirstName} \"{me.Nickname}\" {me.LastName}",
            me.Positions is { Count: > 0 } positions ? PositionHelper.GetDisplayName(positions[0]) : null);
    }

    private static string SenderCity() => $"Bragança, {DateTime.Now.ToString("dd 'de' MMMM 'de' yyyy", Portuguese)}";

    private static string LongDate(DateTime date) => date.ToString("dddd, dd 'de' MMMM 'de' yyyy • HH:mm", Portuguese);

    // ---------- small helpers ----------

    private static MeetingPersonDto Person(ApplicationUser u, Meeting meeting) => new(
        u.Nickname ?? $"{u.FirstName} {u.LastName}",
        u.ProfilePictureSrc,
        meeting.Type == MeetingType.ConselhoVeteranos ? YearsAsTuno(u) : null);

    private static MeetingPersonOptionDto TunoOption(ApplicationUser u) =>
        new(u.Id, $"{u.Nickname ?? $"{u.FirstName} {u.LastName}"} - {YearsAsTuno(u)}");

    /// <summary>The old "x anos y meses de Tuno", from YearTuno / MonthTuno.</summary>
    public static string YearsAsTuno(ApplicationUser user)
    {
        if (user.YearTuno == null)
        {
            return "< 1 ano de Tuno";
        }

        var now = DateTime.Now;
        var start = new DateTime(user.YearTuno.Value, user.MonthTuno ?? 1, 1);
        var totalMonths = ((now.Year - start.Year) * 12) + (now.Month - start.Month);
        if (totalMonths < 1)
        {
            return "< 1 mês de Tuno";
        }

        var years = totalMonths / 12;
        var months = totalMonths % 12;
        if (years == 0)
        {
            return months == 1 ? "1 mês de Tuno" : $"{months} meses de Tuno";
        }

        if (months == 0)
        {
            return years == 1 ? "1 ano de Tuno" : $"{years} anos de Tuno";
        }

        return $"{years} {(years == 1 ? "ano" : "anos")} {months} {(months == 1 ? "mês" : "meses")} de Tuno";
    }

    /// <summary>The old ata view's names: First "Nickname" Last.</summary>
    private static string FullName(ApplicationUser user)
    {
        var first = user.FirstName ?? string.Empty;
        var last = user.LastName ?? string.Empty;
        return string.IsNullOrEmpty(user.Nickname) ? $"{first} {last}".Trim() : $"{first} \"{user.Nickname}\" {last}".Trim();
    }

    public static string TypeLabel(MeetingType type) => type switch
    {
        MeetingType.AssembleiaGeralOrdinaria => "Assembleia Geral Ordinária",
        MeetingType.AssembleiaGeralExtraordinaria => "Assembleia Geral Extraordinária",
        MeetingType.ConselhoVeteranos => "Conselho de Veteranos",
        MeetingType.ReuniaoDirecao => "Reunião de Direção",
        _ => string.Empty,
    };

    private static string Kind(MeetingType type) =>
        type == MeetingType.ConselhoVeteranos ? "cv" : MeetingAccess.IsAssembly(type) ? "ag" : "direcao";

    /// <summary>The create / edit form's choices, labelled as the old select was.</summary>
    private static IReadOnlyList<MeetingTypeOptionDto> TypeOptions(IEnumerable<MeetingType> types) =>
        types.Select(t => new MeetingTypeOptionDto(t.ToString(), t switch
        {
            MeetingType.AssembleiaGeralOrdinaria => "Assembleia Geral Ordinária (AGO)",
            MeetingType.AssembleiaGeralExtraordinaria => "Assembleia Geral Extraordinária (AGE)",
            MeetingType.ConselhoVeteranos => "Conselho de Veteranos (CV)",
            _ => TypeLabel(t),
        })).ToList();

    private static string Format(DateTime value) => value.ToString(DateFormat, CultureInfo.InvariantCulture);

    private static bool TryDate(string? value, out DateTime date) =>
        DateTime.TryParseExact(value?.Trim(), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    private static bool TryType(string? value, out MeetingType type)
    {
        type = default;
        return !string.IsNullOrWhiteSpace(value)
               && !int.TryParse(value, out _)
               && Enum.TryParse(value, ignoreCase: false, out type)
               && Enum.IsDefined(type);
    }

    /// <summary>
    /// The year filter: null = the current fiscal year (the old default), "all" or "" = every year, else "YYYY-YYYY"
    /// (1 September to 31 August).
    /// </summary>
    private static bool TryFiscalYear(string? value, out string? selected, out (DateTime Start, DateTime End)? range)
    {
        selected = null;
        range = null;
        var year = value is null ? FiscalYearHelper.GetCurrentFiscalYearString() : value.Trim();
        if (year.Length == 0 || year == "all")
        {
            return true;
        }

        var parts = year.Split('-');
        if (parts.Length != 2
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var start)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var end)
            || end != start + 1 || start < 2000 || start > 2999)
        {
            return false;
        }

        selected = year;
        range = (new DateTime(start, 9, 1), new DateTime(start + 1, 8, 31));
        return true;
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string? RequiredText(string? value, string field, string requiredMessage, int max, string tooLongMessage, Errors errors)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            errors.Add(field, requiredMessage);
            return null;
        }

        if (text.Length > max)
        {
            errors.Add(field, tooLongMessage);
        }

        return text;
    }

    private static string? OptionalText(string? value, string field, int max, string tooLongMessage, Errors errors)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (text.Length > max)
        {
            errors.Add(field, tooLongMessage);
        }

        return text;
    }

    private static EventResult<T> Invalid<T>(Errors errors) =>
        new(EventResultStatus.Invalid, Errors: errors.ToDictionary());

    /// <summary>Field errors collected before answering, so a form shows every problem at once.</summary>
    private sealed class Errors
    {
        private readonly Dictionary<string, List<string>> _errors = new();

        public bool Any => _errors.Count > 0;

        public void Add(string field, string message)
        {
            if (!_errors.TryGetValue(field, out var messages))
            {
                _errors[field] = messages = new List<string>();
            }

            messages.Add(message);
        }

        public IReadOnlyDictionary<string, string[]> ToDictionary() => _errors.ToDictionary(e => e.Key, e => e.Value.ToArray());
    }
}
