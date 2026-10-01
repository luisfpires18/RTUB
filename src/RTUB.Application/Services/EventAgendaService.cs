using System.Globalization;
using System.Security.Claims;
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
/// The React Events area (React track 011, docs/react-events.md). Reads the existing Events,
/// Enrollments, Trophies, EventVideos, EventRepertoires and Discussions; no schema change.
/// Enrollment writes go through <see cref="IEnrollmentService"/>, so its notifications, category
/// snapshot and retirement update stay exactly as they were.
///
/// Upcoming/past is the old page's rule: an event is past once its last day (EndDate, else Date) is
/// before today. Upcoming soonest first, past newest first, Id breaking ties.
/// </summary>
public sealed class EventAgendaService : IEventAgendaService
{
    public const int MaxNotesLength = 1000;

    private readonly IDbContextFactory<ApplicationDbContext> _contexts;
    private readonly IEnrollmentService _enrollments;
    private readonly IAuditLogService _audit;
    private readonly ILogger<EventAgendaService> _logger;

    public EventAgendaService(
        IDbContextFactory<ApplicationDbContext> contexts,
        IEnrollmentService enrollments,
        IAuditLogService audit,
        ILogger<EventAgendaService> logger)
    {
        _contexts = contexts;
        _enrollments = enrollments;
        _audit = audit;
        _logger = logger;
    }

    public async Task<EventAgendaDto> GetAgendaAsync(ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        // ponytail: whole table in memory (57 events today); page it if it ever reaches thousands.
        var events = await db.Events.AsNoTracking().ToListAsync();
        var extras = await LoadExtrasAsync(db, events.Select(e => e.Id).ToList(), EventsAuthorization.UserId(user));

        return new EventAgendaDto(
            extras.IsMember,
            EventsAuthorization.CanManage(user),
            Upcoming(events).Select(e => ToSummary(e, extras)).ToList(),
            events.Where(IsPast).OrderByDescending(e => e.Date).ThenByDescending(e => e.Id)
                .Select(e => ToSummary(e, extras)).ToList());
    }

    public async Task<IReadOnlyList<UpcomingEventDto>> GetUpcomingPreviewAsync(int count)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var events = await db.Events.AsNoTracking().ToListAsync();
        return Upcoming(events).Take(count).Select(UpcomingEventDto.From).ToList();
    }

    public async Task<EventDetailDto?> GetEventAsync(int id, ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var e = await db.Events.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (e is null)
        {
            return null;
        }

        var extras = await LoadExtrasAsync(db, new List<int> { id }, EventsAuthorization.UserId(user));
        var videos = (await db.EventVideos.AsNoTracking().Where(v => v.EventId == id).ToListAsync())
            .Where(v => IsSafeUrl(v.Url))
            .OrderBy(v => v.SortOrder).ThenBy(v => v.Id)
            .Select((v, i) => new EventVideoDto(v.Id, string.IsNullOrWhiteSpace(v.Title) ? $"Vídeo {i + 1}" : v.Title!, v.Url, v.MimeType))
            .ToList();

        EventMemberDetailDto? member = null;
        if (extras.IsMember)
        {
            var notGoing = await db.Enrollments.CountAsync(x => x.EventId == id && !x.WillAttend);
            var repertoire = (await db.EventRepertoires.AsNoTracking()
                    .Where(r => r.EventId == id)
                    .Select(r => new { r.RepertoireDate, r.DisplayOrder, r.Id, Title = r.Song!.Title })
                    .ToListAsync())
                .GroupBy(r => r.RepertoireDate.Date)
                .OrderBy(g => g.Key)
                .Select(g => new EventRepertoireDayDto(
                    DateText(g.Key),
                    g.OrderBy(r => r.DisplayOrder).ThenBy(r => r.Id).Select(r => r.Title).ToList()))
                .ToList();
            member = new EventMemberDetailDto(e.IsCancelled ? e.CancellationReason : null, notGoing, repertoire,
                await ParticipantsAsync(db, id));
        }

        return new EventDetailDto(extras.IsMember, EventsAuthorization.CanManage(user), ToSummary(e, extras), videos, member);
    }

    public async Task<EventResult<EventEnrollmentDto>> GetEnrollmentAsync(int id, ClaimsPrincipal user)
    {
        var userId = EventsAuthorization.UserId(user);
        if (userId is null)
        {
            return EventResult<EventEnrollmentDto>.Fail(EventResultStatus.SignInRequired);
        }

        var context = await LoadEnrollmentAsync(id, userId);
        return context is null
            ? EventResult<EventEnrollmentDto>.Fail(EventResultStatus.NotFound)
            : EventResult<EventEnrollmentDto>.Ok(context.ToDto());
    }

    public async Task<EventResult<EventEnrollmentDto>> SaveEnrollmentAsync(int id, EventEnrollmentInput input, ClaimsPrincipal user)
    {
        var userId = EventsAuthorization.UserId(user);
        if (userId is null)
        {
            return EventResult<EventEnrollmentDto>.Fail(EventResultStatus.SignInRequired);
        }

        var context = await LoadEnrollmentAsync(id, userId);
        if (context is null)
        {
            return EventResult<EventEnrollmentDto>.Fail(EventResultStatus.NotFound);
        }

        if (context.State != "open")
        {
            return EventResult<EventEnrollmentDto>.Fail(EventResultStatus.Closed);
        }

        var notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim();
        if (notes?.Length > MaxNotesLength)
        {
            return EventResult<EventEnrollmentDto>.Invalid("notes", $"A nota não pode exceder {MaxNotesLength} caracteres.");
        }

        // Only someone who is going plays; the choice must be one the page offered.
        InstrumentType? instrument = null;
        if (input.WillAttend && !string.IsNullOrWhiteSpace(input.Instrument))
        {
            if (!Enum.TryParse<InstrumentType>(input.Instrument, out var offered) || !context.Options.Contains(offered))
            {
                return EventResult<EventEnrollmentDto>.Invalid("instrument", "Escolha um dos instrumentos indicados.");
            }

            instrument = offered;
        }

        // As the Blazor page did: the member's other instruments, by display name.
        var others = instrument is { } chosen
            ? string.Join(", ", context.Registered.Where(i => i != chosen).Select(StatusHelper.GetInstrumentDisplay))
            : null;
        others = string.IsNullOrEmpty(others) ? null : others;

        if (context.Enrollment is { } existing)
        {
            await _enrollments.UpdateEnrollmentAsync(existing.Id, input.WillAttend, instrument, notes, others);
        }
        else
        {
            await _enrollments.CreateEnrollmentAsync(userId, id, instrument, notes, input.WillAttend, others);
        }

        return EventResult<EventEnrollmentDto>.Ok((await LoadEnrollmentAsync(id, userId))!.ToDto());
    }

    public async Task<EventResult<EventEnrollmentDto>> RemoveEnrollmentAsync(int id, ClaimsPrincipal user)
    {
        var userId = EventsAuthorization.UserId(user);
        if (userId is null)
        {
            return EventResult<EventEnrollmentDto>.Fail(EventResultStatus.SignInRequired);
        }

        var context = await LoadEnrollmentAsync(id, userId);
        if (context is null)
        {
            return EventResult<EventEnrollmentDto>.Fail(EventResultStatus.NotFound);
        }

        if (!context.CanRemove)
        {
            return EventResult<EventEnrollmentDto>.Fail(EventResultStatus.Closed);
        }

        await _enrollments.DeleteEnrollmentAsync(context.Enrollment!.Id);
        return EventResult<EventEnrollmentDto>.Ok((await LoadEnrollmentAsync(id, userId))!.ToDto());
    }

    public async Task<bool> RecordVideoPlayAsync(int videoId, ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var video = await db.EventVideos.AsNoTracking()
            .Where(v => v.Id == videoId)
            .Select(v => new { v.Id, v.Title, EventName = v.Event.Name })
            .FirstOrDefaultAsync();
        if (video is null)
        {
            return false;
        }

        try
        {
            await _audit.AddAsync(new AuditLog
            {
                EntityType = "EventVideo",
                EntityId = video.Id,
                Action = "Played",
                UserId = EventsAuthorization.UserId(user),
                UserName = EventsAuthorization.IsMember(user) ? user.Identity!.Name : null,
                Timestamp = DateTime.UtcNow,
                EntityDisplayName = $"{video.Title ?? "Video"} ({video.EventName})",
            });
        }
        catch (Exception ex)
        {
            // A play is never refused because its audit row could not be written.
            _logger.LogWarning(ex, "Failed to audit an event video play ({VideoId})", videoId);
        }

        return true;
    }

    private static async Task<EventParticipantsDto> ParticipantsAsync(ApplicationDbContext db, int eventId)
    {
        var rows = await db.Enrollments.AsNoTracking()
            .Where(x => x.EventId == eventId && x.User != null)
            .OrderByDescending(x => x.EnrolledAt).ThenByDescending(x => x.Id)
            .Select(x => new
            {
                x.WillAttend,
                x.Instrument,
                x.Notes,
                x.CategoryAtEvent,
                x.User!.Nickname,
                x.User.FirstName,
                x.User.LastName,
                x.User.ImageUrl,
                x.User.Categories,
                x.User.Positions,
            })
            .ToListAsync();

        var people = rows.Select(r =>
        {
            // The category at the event when it was recorded, else the member's current one (as before).
            var category = r.CategoryAtEvent
                ?? new ApplicationUser { Categories = r.Categories ?? new List<MemberCategory>() }.GetPrimaryCategory();
            var fullName = $"{r.FirstName} {r.LastName}".Trim();
            var dto = new EventParticipantDto(
                string.IsNullOrWhiteSpace(r.Nickname) ? (string.IsNullOrEmpty(fullName) ? "Membro" : fullName) : r.Nickname,
                string.IsNullOrEmpty(fullName) ? null : fullName,
                IsSafeUrl(r.ImageUrl) ? r.ImageUrl! : DefaultAvatar,
                r.Positions?.Contains(Position.Magister) == true ? "MAGISTER" : category is { } c ? StatusHelper.GetCategoryDisplay(c) : null,
                r.WillAttend && r.Instrument is { } i ? StatusHelper.GetInstrumentDisplay(i) : null,
                string.IsNullOrWhiteSpace(r.Notes) ? null : r.Notes);
            return (r.WillAttend, Leitao: category == MemberCategory.Leitao, dto);
        }).ToList();

        return new EventParticipantsDto(
            people.Where(p => p.WillAttend && !p.Leitao).Select(p => p.dto).ToList(),
            people.Where(p => p.WillAttend && p.Leitao).Select(p => p.dto).ToList(),
            people.Where(p => !p.WillAttend).Select(p => p.dto).ToList());
    }

    private const string DefaultAvatar = "/images/default-avatar.webp";

    // ---------- rules ----------

    private static DateTime LastDay(Event e) => (e.EndDate ?? e.Date).Date;

    private static bool IsPast(Event e) => LastDay(e) < DateTime.Today;

    private static IEnumerable<Event> Upcoming(IEnumerable<Event> events) =>
        events.Where(e => !IsPast(e)).OrderBy(e => e.Date).ThenBy(e => e.Id);

    /// <summary>September to August, as the old fiscal-year filter.</summary>
    private static string Season(DateTime date)
    {
        var start = date.Month >= 9 ? date.Year : date.Year - 1;
        return $"{start}-{start + 1}";
    }

    // ---------- mapping ----------

    private sealed record Extras(
        bool IsMember,
        ILookup<int, string> Trophies,
        IReadOnlyDictionary<int, int> Videos,
        IReadOnlyDictionary<int, int> Going,
        IReadOnlyDictionary<int, int> Repertoire,
        IReadOnlyDictionary<int, int> Discussion,
        IReadOnlyDictionary<int, bool> Mine);

    private static async Task<Extras> LoadExtrasAsync(ApplicationDbContext db, List<int> ids, string? userId)
    {
        var trophies = (await db.Trophies.AsNoTracking().Where(t => ids.Contains(t.EventId))
                .Select(t => new { t.EventId, t.Name, t.Id }).ToListAsync())
            .OrderBy(t => t.Name, StringComparer.CurrentCulture).ThenBy(t => t.Id)
            .ToLookup(t => t.EventId, t => t.Name);
        var videos = await CountAsync(db.EventVideos.Where(v => ids.Contains(v.EventId)).Select(v => v.EventId));

        if (userId is null)
        {
            var none = new Dictionary<int, int>();
            return new Extras(false, trophies, videos, none, none, none, new Dictionary<int, bool>());
        }

        var going = await CountAsync(db.Enrollments.Where(x => ids.Contains(x.EventId) && x.WillAttend).Select(x => x.EventId));
        var repertoire = await CountAsync(db.EventRepertoires.Where(r => ids.Contains(r.EventId)).Select(r => r.EventId));
        var discussion = await CountAsync(db.Posts.Where(p => !p.IsDeleted)
            .Join(db.Discussions.Where(d => ids.Contains(d.EventId)), p => p.DiscussionId, d => d.Id, (p, d) => d.EventId));
        var mine = await db.Enrollments.AsNoTracking()
            .Where(x => x.UserId == userId && ids.Contains(x.EventId))
            .ToDictionaryAsync(x => x.EventId, x => x.WillAttend);

        return new Extras(true, trophies, videos, going, repertoire, discussion, mine);
    }

    private static async Task<IReadOnlyDictionary<int, int>> CountAsync(IQueryable<int> eventIds) =>
        (await eventIds.ToListAsync()).GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());

    private static EventSummaryDto ToSummary(Event e, Extras extras) => new(
        e.Id,
        e.Name,
        DateText(e.Date),
        e.Date.TimeOfDay == TimeSpan.Zero ? null : e.Date.ToString("HH:mm", CultureInfo.InvariantCulture),
        e.EndDate is { } end && end.Date > e.Date.Date ? DateText(end) : null,
        e.Location,
        e.Type.GetDisplayName(),
        e.IsCancelled,
        IsPast(e),
        Season(e.Date),
        IsSafeUrl(e.ImageUrl) ? e.ImageUrl : null,
        extras.Videos.GetValueOrDefault(e.Id),
        extras.Trophies[e.Id].ToList(),
        extras.IsMember
            ? new EventMemberSummaryDto(
                e.Description,
                extras.Mine.TryGetValue(e.Id, out var going) ? (going ? "going" : "notGoing") : null,
                extras.Going.GetValueOrDefault(e.Id),
                extras.Repertoire.GetValueOrDefault(e.Id),
                extras.Discussion.GetValueOrDefault(e.Id))
            : null);

    private static string DateText(DateTime date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>An https URL or a same-site path; anything else (http:, javascript:, data:, //host) is dropped.</summary>
    private static bool IsSafeUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && ((url.StartsWith('/') && !url.StartsWith("//") && !url.StartsWith("/\\"))
            || (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps));

    // ---------- enrollment ----------

    private sealed record EnrollmentContext(
        EventSummaryDto Event,
        string State,
        Enrollment? Enrollment,
        bool IsLeitao,
        IReadOnlyList<InstrumentType> Registered,
        IReadOnlyList<InstrumentType> Options,
        InstrumentType? Default)
    {
        public bool CanRemove => State == "past" && Enrollment?.WillAttend == true;

        public EventEnrollmentDto ToDto() => new(
            Event,
            State,
            Enrollment is null ? null : Enrollment.WillAttend ? "going" : "notGoing",
            Enrollment?.WillAttend == true ? Enrollment.Instrument?.ToString() : null,
            Enrollment?.Notes,
            IsLeitao,
            Options.Select(i => new EventInstrumentOptionDto(i.ToString(), StatusHelper.GetInstrumentDisplay(i))).ToList(),
            Default?.ToString(),
            CanRemove);
    }

    /// <summary>
    /// The rules of the old enrolment buttons: an upcoming event that is not cancelled is open
    /// (going / not going, instrument, note); a past one only lets someone who went withdraw;
    /// a cancelled one takes no answers. Null when the event does not exist.
    /// </summary>
    private async Task<EnrollmentContext?> LoadEnrollmentAsync(int id, string userId)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var e = await db.Events.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (e is null)
        {
            return null;
        }

        var extras = await LoadExtrasAsync(db, new List<int> { id }, userId);
        var enrollment = await db.Enrollments.AsNoTracking().FirstOrDefaultAsync(x => x.EventId == id && x.UserId == userId);
        var categories = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.Categories).FirstOrDefaultAsync();
        var isLeitao = categories?.Contains(MemberCategory.Leitao) == true;
        var registered = await db.MemberInstruments.AsNoTracking()
            .Where(i => i.MemberId == userId)
            .OrderByDescending(i => i.IsPrimary).ThenBy(i => i.Id)
            .Select(i => i.InstrumentType)
            .ToListAsync();

        // The old form offered the member's own instruments; a Leitão with none could pick any.
        var options = registered.Count > 0 ? registered : isLeitao ? Enum.GetValues<InstrumentType>().ToList() : new List<InstrumentType>();
        var state = e.IsCancelled ? "cancelled" : IsPast(e) ? "past" : "open";

        return new EnrollmentContext(
            ToSummary(e, extras), state, enrollment, isLeitao, registered, options,
            registered.Count > 0 ? registered[0] : null);
    }
}
