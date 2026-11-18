using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// The image, cancel / reactivate and notice actions behind the React agenda
/// (docs/react-events.md). Rules enforced here:
/// Admin or Owner only; cancel, reactivate and notices only for an event whose last day has not
/// passed; notices never for a cancelled event. Audiences
/// are: email to confirmed addresses with "Notificações por email" on, push to
/// members with an active subscription (optionally Leitões and Caloiros only). No schema change.
/// </summary>
public sealed partial class EventAdminService : IEventAdminService
{
    private readonly TimeProvider _time;

    public const long MaxImageBytes = 5 * 1024 * 1024;

    public const int MaxReasonLength = 1000;

    public const int MaxPushLength = 500;

    private static readonly string[] ImageTypes = { "image/webp", "image/jpeg", "image/png" };

    private readonly IDbContextFactory<ApplicationDbContext> _contexts;

    private readonly IEventService _events;

    private readonly IEmailNotificationService _email;

    private readonly IPushNotificationService _push;

    private readonly IPushNotificationFactory _pushFactory;

    private readonly IAuditLogService _audit;

    private readonly ITrophyService _trophies;

    private readonly ILogger<EventAdminService> _logger;

    public EventAdminService(
        IDbContextFactory<ApplicationDbContext> contexts,
        IEventService events,
        IEmailNotificationService email,
        IPushNotificationService push,
        IPushNotificationFactory pushFactory,
        IAuditLogService audit,
        ITrophyService trophies,
        ILogger<EventAdminService> logger,
        TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
        _contexts = contexts;
        _events = events;
        _email = email;
        _push = push;
        _pushFactory = pushFactory;
        _audit = audit;
        _trophies = trophies;
        _logger = logger;
    }

    // ---------- image ----------

    public async Task<EventResult<bool>> SetImageAsync(int id, EventImageUpload image, ClaimsPrincipal user)
    {
        if (EditRefusal<bool>(user) is { } refused)
        {
            return refused;
        }

        if (await FindAsync(id) is null)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        if (image.Length <= 0 || !ImageTypes.Contains(image.ContentType, StringComparer.OrdinalIgnoreCase) || !LooksLikeImage(image.Content))
        {
            return EventResult<bool>.Invalid("image", "A imagem tem de ser WebP, JPEG ou PNG.");
        }

        if (image.Length > MaxImageBytes)
        {
            return EventResult<bool>.Invalid("image", "A imagem não pode exceder 5 MB.");
        }

        // Crop and save: the previous image is deleted and the new one stored under the
        // existing images/{env}/events/{name}_{timestamp}.webp key.
        await _events.SetEventImageAsync(id, image.Content, image.FileName, image.ContentType);
        return EventResult<bool>.Ok(true);
    }

    public async Task<EventResult<bool>> RemoveImageAsync(int id, ClaimsPrincipal user)
    {
        if (EditRefusal<bool>(user) is { } refused)
        {
            return refused;
        }

        if (await FindAsync(id) is null)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        await _events.RemoveEventImageAsync(id);
        return EventResult<bool>.Ok(true);
    }

    /// <summary>The declared type is the browser's word; the first bytes must agree (WebP, JPEG or PNG).</summary>
    private static bool LooksLikeImage(Stream content)
    {
        if (!content.CanSeek)
        {
            return false;
        }

        var head = new byte[12];
        var read = content.Read(head, 0, head.Length);
        content.Position = 0;
        return read == 12 && (
            (head[0] == 'R' && head[1] == 'I' && head[2] == 'F' && head[3] == 'F' && head[8] == 'W' && head[9] == 'E' && head[10] == 'B' && head[11] == 'P')
            || (head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF)
            || (head[0] == 0x89 && head[1] == 'P' && head[2] == 'N' && head[3] == 'G'));
    }

    // ---------- cancel / reactivate ----------

    public async Task<EventResult<EventNoticeResultDto>> CancelAsync(int id, EventCancelInput input, ClaimsPrincipal user, string baseUrl)
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

        var reason = input.Reason?.Trim();
        if (string.IsNullOrEmpty(reason))
        {
            return EventResult<EventNoticeResultDto>.Invalid("reason", "O motivo do cancelamento é obrigatório.");
        }

        if (reason.Length > MaxReasonLength)
        {
            return EventResult<EventNoticeResultDto>.Invalid("reason", $"O motivo não pode exceder {MaxReasonLength} caracteres.");
        }

        // Cancelling deletes every enrollment of the event.
        await _events.CancelEventAsync(id, reason);

        if (!input.NotifyByEmail)
        {
            return EventResult<EventNoticeResultDto>.Ok(new EventNoticeResultDto(0, 0, null));
        }

        // The event is cancelled whatever happens to the emails; a failure is reported, not thrown.
        var recipients = await EmailRecipientsAsync();
        var sent = await SendEmailAsync(() => _email.SendEventCancellationNotificationAsync(
            e.Id, e.Name, e.Date, e.Location, reason, EventsLink(baseUrl), recipients.Emails, recipients.Data, e.EndDate), e.Id);
        return EventResult<EventNoticeResultDto>.Ok(sent.Error is null
            ? new EventNoticeResultDto(sent.Count, 0, sent.Warning)
            : new EventNoticeResultDto(0, recipients.Emails.Count, $"A atuação foi cancelada, mas os emails não foram enviados: {sent.Error}"));
    }

    public async Task<EventResult<bool>> ReactivateAsync(int id, ClaimsPrincipal user)
    {
        if (Refusal<bool>(user) is { } refused)
        {
            return refused;
        }

        var e = await FindAsync(id);
        if (e is null)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        if (!e.IsCancelled || IsPast(e, Today))
        {
            return EventResult<bool>.Fail(EventResultStatus.Closed);
        }

        await _events.UncancelEventAsync(id);
        return EventResult<bool>.Ok(true);
    }
}
