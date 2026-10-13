using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Extensions;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// The old /member/events image, cancel / reactivate and notice actions behind the React agenda
/// (React track 012A, docs/react-events.md). Same rules as the old page, now enforced here:
/// Admin or Owner only; cancel, reactivate and notices only for an event whose last day has not
/// passed (the old page showed them only there); notices never for a cancelled event. Audiences
/// are the old page's: email to confirmed addresses with "Notificações por email" on, push to
/// members with an active subscription (optionally Leitões and Caloiros only). No schema change.
/// </summary>
public sealed class EventAdminService : IEventAdminService
{
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
        ILogger<EventAdminService> logger)
    {
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
        if (Refusal<bool>(user) is { } refused)
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

        // The old crop-and-save path: the previous image is deleted and the new one stored under the
        // existing images/{env}/events/{name}_{timestamp}.webp key.
        await _events.SetEventImageAsync(id, image.Content, image.FileName, image.ContentType);
        return EventResult<bool>.Ok(true);
    }

    public async Task<EventResult<bool>> RemoveImageAsync(int id, ClaimsPrincipal user)
    {
        if (Refusal<bool>(user) is { } refused)
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

        if (e.IsCancelled || IsPast(e))
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

        // As before: cancelling deletes every enrollment of the event.
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

        if (!e.IsCancelled || IsPast(e))
        {
            return EventResult<bool>.Fail(EventResultStatus.Closed);
        }

        await _events.UncancelEventAsync(id);
        return EventResult<bool>.Ok(true);
    }

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

        if (e.IsCancelled || IsPast(e))
        {
            return EventResult<EventNoticeResultDto>.Fail(EventResultStatus.Closed);
        }

        return input.Channel switch
        {
            "email" => await SendEmailNoticeAsync(e, input, baseUrl),
            "push" => await SendPushNoticeAsync(e, input, user, baseUrl),
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

    private async Task<EventResult<EventNoticeResultDto>> SendPushNoticeAsync(Event e, EventNoticeInput input, ClaimsPrincipal user, string baseUrl)
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

        var (sent, failed) = await _push.SendToSelectedUsersAsync(recipients, _pushFactory.CreateEventCustomNotification(e, message, baseUrl));

        // Audited as the old page did; an audit failure never turns a sent notice into an error.
        try
        {
            await _audit.AddAsync(new AuditLog
            {
                EntityType = "Event",
                EntityId = e.Id,
                Action = "PushNotificationSent",
                UserId = EventsAuthorization.UserId(user),
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

    // ---------- prizes (012B) ----------

    public const int MaxPrizeNameLength = 200;

    /// <summary>
    /// Where a prize can be added: a festival whose last day has passed. The old page only offered the
    /// prizes button on past festivals; existing prizes stay editable and deletable on any event.
    /// </summary>
    public static bool TakesPrizes(Event e) => e.Type == EventType.Festival && IsPast(e);

    public async Task<EventResult<IReadOnlyList<EventPrizeDto>>> GetPrizesAsync(int id, ClaimsPrincipal user)
    {
        if (Refusal<IReadOnlyList<EventPrizeDto>>(user) is { } refused)
        {
            return refused;
        }

        return await FindAsync(id) is null
            ? EventResult<IReadOnlyList<EventPrizeDto>>.Fail(EventResultStatus.NotFound)
            : EventResult<IReadOnlyList<EventPrizeDto>>.Ok(await PrizesAsync(id));
    }

    public async Task<EventResult<IReadOnlyList<EventPrizeDto>>> AddPrizeAsync(int id, EventPrizeInput input, ClaimsPrincipal user)
    {
        if (Refusal<IReadOnlyList<EventPrizeDto>>(user) is { } refused)
        {
            return refused;
        }

        var e = await FindAsync(id);
        if (e is null)
        {
            return EventResult<IReadOnlyList<EventPrizeDto>>.Fail(EventResultStatus.NotFound);
        }

        if (!TakesPrizes(e))
        {
            return EventResult<IReadOnlyList<EventPrizeDto>>.Fail(EventResultStatus.Closed);
        }

        if (PrizeNameError(input) is { } invalid)
        {
            return invalid;
        }

        await _trophies.CreateAsync(Trophy.Create(input.Name!.Trim(), id));
        return EventResult<IReadOnlyList<EventPrizeDto>>.Ok(await PrizesAsync(id));
    }

    public async Task<EventResult<IReadOnlyList<EventPrizeDto>>> UpdatePrizeAsync(int id, int prizeId, EventPrizeInput input, ClaimsPrincipal user)
    {
        if (Refusal<IReadOnlyList<EventPrizeDto>>(user) is { } refused)
        {
            return refused;
        }

        if (!await PrizeOfEventAsync(id, prizeId))
        {
            return EventResult<IReadOnlyList<EventPrizeDto>>.Fail(EventResultStatus.NotFound);
        }

        if (PrizeNameError(input) is { } invalid)
        {
            return invalid;
        }

        // TrophyService.UpdateAsync loads the row by id and only changes its name.
        var renamed = new Trophy(id) { Id = prizeId, Name = input.Name!.Trim() };
        await _trophies.UpdateAsync(renamed);
        return EventResult<IReadOnlyList<EventPrizeDto>>.Ok(await PrizesAsync(id));
    }

    public async Task<EventResult<IReadOnlyList<EventPrizeDto>>> DeletePrizeAsync(int id, int prizeId, ClaimsPrincipal user)
    {
        if (Refusal<IReadOnlyList<EventPrizeDto>>(user) is { } refused)
        {
            return refused;
        }

        if (!await PrizeOfEventAsync(id, prizeId))
        {
            return EventResult<IReadOnlyList<EventPrizeDto>>.Fail(EventResultStatus.NotFound);
        }

        await _trophies.DeleteAsync(prizeId);
        return EventResult<IReadOnlyList<EventPrizeDto>>.Ok(await PrizesAsync(id));
    }

    private static EventResult<IReadOnlyList<EventPrizeDto>>? PrizeNameError(EventPrizeInput input)
    {
        var name = input.Name?.Trim();
        return string.IsNullOrEmpty(name) ? EventResult<IReadOnlyList<EventPrizeDto>>.Invalid("name", "Indique o nome do prémio.")
            : name.Length > MaxPrizeNameLength ? EventResult<IReadOnlyList<EventPrizeDto>>.Invalid("name", $"O nome não pode ter mais de {MaxPrizeNameLength} caracteres.")
            : null;
    }

    /// <summary>A prize id only counts under its own event: no editing another event's prize through this one.</summary>
    private async Task<bool> PrizeOfEventAsync(int id, int prizeId)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        return await db.Trophies.AnyAsync(t => t.Id == prizeId && t.EventId == id);
    }

    /// <summary>The agenda's order: by name (current culture), then id.</summary>
    private async Task<IReadOnlyList<EventPrizeDto>> PrizesAsync(int id)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        return (await db.Trophies.AsNoTracking().Where(t => t.EventId == id).Select(t => new { t.Id, t.Name }).ToListAsync())
            .OrderBy(t => t.Name, StringComparer.CurrentCulture).ThenBy(t => t.Id)
            .Select(t => new EventPrizeDto(t.Id, t.Name))
            .ToList();
    }

    // ---------- videos (012C) ----------

    public const long MaxVideoBytes = 100 * 1024 * 1024;
    public const int MaxVideoTitleLength = 200;

    private static readonly string[] VideoExtensions = { ".mp4", ".mov", ".m4v", ".webm", ".3gp", ".mkv", ".avi" };

    public async Task<EventResult<IReadOnlyList<EventManagedVideoDto>>> GetVideosAsync(int id, ClaimsPrincipal user)
    {
        if (Refusal<IReadOnlyList<EventManagedVideoDto>>(user) is { } refused)
        {
            return refused;
        }

        return await FindAsync(id) is null
            ? EventResult<IReadOnlyList<EventManagedVideoDto>>.Fail(EventResultStatus.NotFound)
            : EventResult<IReadOnlyList<EventManagedVideoDto>>.Ok(await VideosAsync(id));
    }

    public async Task<EventResult<IReadOnlyList<EventManagedVideoDto>>> AddVideoAsync(int id, EventVideoUpload upload, ClaimsPrincipal user)
    {
        if (Refusal<IReadOnlyList<EventManagedVideoDto>>(user) is { } refused)
        {
            return refused;
        }

        var e = await FindAsync(id);
        if (e is null)
        {
            return EventResult<IReadOnlyList<EventManagedVideoDto>>.Fail(EventResultStatus.NotFound);
        }

        // The old page offered uploads only on past events (the videos button of the archive cards).
        if (!IsPast(e))
        {
            return EventResult<IReadOnlyList<EventManagedVideoDto>>.Fail(EventResultStatus.Closed);
        }

        var extension = Path.GetExtension(upload.FileName ?? string.Empty).ToLowerInvariant();
        var isVideo = upload.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) || VideoExtensions.Contains(extension);
        if (upload.Length <= 0 || !isVideo)
        {
            return EventResult<IReadOnlyList<EventManagedVideoDto>>.Invalid("file", "Escolha um ficheiro de vídeo.");
        }

        if (upload.Length > MaxVideoBytes)
        {
            return EventResult<IReadOnlyList<EventManagedVideoDto>>.Invalid("file", "O vídeo não pode exceder 100 MB.");
        }

        // The old form required a title before it would send.
        var title = upload.Title?.Trim();
        if (string.IsNullOrEmpty(title))
        {
            return EventResult<IReadOnlyList<EventManagedVideoDto>>.Invalid("title", "Indique o título do vídeo.");
        }

        if (title.Length > MaxVideoTitleLength)
        {
            return EventResult<IReadOnlyList<EventManagedVideoDto>>.Invalid("title", $"O título não pode ter mais de {MaxVideoTitleLength} caracteres.");
        }

        // Storage key events/{env}/videos/{eventId}_{timestamp}_{file}, the next sort position and the
        // push to the other members all come from the existing EventService, as on the old page.
        await _events.AddVideoAsync(id, upload.Content, upload.FileName!, upload.ContentType, EventsAuthorization.UserId(user)!, title);
        return EventResult<IReadOnlyList<EventManagedVideoDto>>.Ok(await VideosAsync(id));
    }

    public async Task<EventResult<IReadOnlyList<EventManagedVideoDto>>> RenameVideoAsync(int id, int videoId, EventVideoTitleInput input, ClaimsPrincipal user)
    {
        if (Refusal<IReadOnlyList<EventManagedVideoDto>>(user) is { } refused)
        {
            return refused;
        }

        if (!await VideoOfEventAsync(id, videoId))
        {
            return EventResult<IReadOnlyList<EventManagedVideoDto>>.Fail(EventResultStatus.NotFound);
        }

        var title = input.Title?.Trim();
        if (title is { Length: > MaxVideoTitleLength })
        {
            return EventResult<IReadOnlyList<EventManagedVideoDto>>.Invalid("title", $"O título não pode ter mais de {MaxVideoTitleLength} caracteres.");
        }

        await _events.UpdateVideoTitleAsync(videoId, string.IsNullOrEmpty(title) ? null : title, EventsAuthorization.UserId(user)!, isAdmin: true);
        return EventResult<IReadOnlyList<EventManagedVideoDto>>.Ok(await VideosAsync(id));
    }

    public async Task<EventResult<IReadOnlyList<EventManagedVideoDto>>> ReorderVideosAsync(int id, EventVideoOrderInput input, ClaimsPrincipal user)
    {
        if (Refusal<IReadOnlyList<EventManagedVideoDto>>(user) is { } refused)
        {
            return refused;
        }

        if (await FindAsync(id) is null)
        {
            return EventResult<IReadOnlyList<EventManagedVideoDto>>.Fail(EventResultStatus.NotFound);
        }

        // The whole list, each video once: a stale or partial order is refused rather than half applied.
        var current = (await VideosAsync(id)).Select(v => v.Id).ToHashSet();
        var order = input.VideoIds ?? Array.Empty<int>();
        if (order.Count != current.Count || order.Distinct().Count() != order.Count || !order.All(current.Contains))
        {
            return EventResult<IReadOnlyList<EventManagedVideoDto>>.Invalid("videoIds", "A lista de vídeos mudou entretanto. Recarregue e tente outra vez.");
        }

        await _events.UpdateVideoOrderAsync(id, order.ToList());
        return EventResult<IReadOnlyList<EventManagedVideoDto>>.Ok(await VideosAsync(id));
    }

    public async Task<EventResult<IReadOnlyList<EventManagedVideoDto>>> DeleteVideoAsync(int id, int videoId, ClaimsPrincipal user)
    {
        if (Refusal<IReadOnlyList<EventManagedVideoDto>>(user) is { } refused)
        {
            return refused;
        }

        if (!await VideoOfEventAsync(id, videoId))
        {
            return EventResult<IReadOnlyList<EventManagedVideoDto>>.Fail(EventResultStatus.NotFound);
        }

        // Deletes the stored file first (only what this environment owns; a storage failure is logged
        // and the row still goes), then the row - EventService.DeleteVideoAsync, unchanged.
        await _events.DeleteVideoAsync(videoId, EventsAuthorization.UserId(user)!, isAdmin: true);
        return EventResult<IReadOnlyList<EventManagedVideoDto>>.Ok(await VideosAsync(id));
    }

    private async Task<bool> VideoOfEventAsync(int id, int videoId)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        return await db.EventVideos.AnyAsync(v => v.Id == videoId && v.EventId == id);
    }

    /// <summary>The page's order: SortOrder, then id.</summary>
    private async Task<IReadOnlyList<EventManagedVideoDto>> VideosAsync(int id)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        return await db.EventVideos.AsNoTracking().Where(v => v.EventId == id)
            .OrderBy(v => v.SortOrder).ThenBy(v => v.Id)
            .Select(v => new EventManagedVideoDto(v.Id, v.Title))
            .ToListAsync();
    }

    // ---------- helpers ----------

    private sealed record Recipients(List<string> Emails, Dictionary<string, (string nickname, string fullName)> Data);

    /// <summary>The old page's email audience: confirmed addresses with "Notificações por email" on.</summary>
    private async Task<Recipients> EmailRecipientsAsync()
    {
        var people = (await UsersAsync())
            .Where(u => u.Subscribed && u.EmailConfirmed && !string.IsNullOrWhiteSpace(u.Email))
            .DistinctBy(u => u.Email!, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new Recipients(
            people.Select(u => u.Email!).ToList(),
            people.ToDictionary(u => u.Email!, u => (u.Nickname ?? "", $"{u.FirstName ?? ""} {u.LastName ?? ""}".Trim())));
    }

    /// <summary>Runs one email send; a thrown error or a refusal comes back as text for the modal.</summary>
    private async Task<(int Count, string? Warning, string? Error)> SendEmailAsync(
        Func<Task<(bool success, int count, string? errorMessage)>> send, int eventId)
    {
        try
        {
            var (success, count, error) = await send();
            return success ? (count, error, null) : (0, null, error ?? "Não foi possível enviar os emails.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send event emails for event {EventId}", eventId);
            return (0, null, "Não foi possível enviar os emails.");
        }
    }

    // ponytail: whole Users table in memory, as the old page did (~100 rows); filter in SQL if it grows.
    private async Task<List<ApplicationUser>> UsersAsync()
    {
        await using var db = await _contexts.CreateDbContextAsync();
        return await db.Users.AsNoTracking().ToListAsync();
    }

    private async Task<Event?> FindAsync(int id)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        return await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
    }

    /// <summary>The same "past" as the agenda: the last day (EndDate, else Date) is before today.</summary>
    private static bool IsPast(Event e) => (e.EndDate ?? e.Date).Date < DateTime.Today;

    /// <summary>Where the old emails pointed: the agenda.</summary>
    private static string EventsLink(string baseUrl) => $"{baseUrl.TrimEnd('/')}/events";

    private static EventResult<T>? Refusal<T>(ClaimsPrincipal user) =>
        !EventsAuthorization.IsMember(user) ? EventResult<T>.Fail(EventResultStatus.SignInRequired)
        : !EventsAuthorization.CanManage(user) ? EventResult<T>.Fail(EventResultStatus.Forbidden)
        : null;
}
