using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// The React Events area (docs/react-events.md). Reads the existing Events,
/// Enrollments, Trophies, EventVideos, EventRepertoires and Discussions; no schema change.
/// Enrollment writes go through <see cref="IEnrollmentService"/>, so its notifications, category
/// snapshot and retirement update stay exactly as they were.
///
/// Upcoming/past: an event is past once its last day (EndDate, else Date) is
/// before today in Portugal (<see cref="PortugalTime"/>, not the server's zone). Upcoming soonest first,
/// past newest first, Id breaking ties.
/// </summary>
public sealed partial class EventAgendaService : IEventAgendaService
{
    public const int MaxNotesLength = 1000;

    private readonly IDbContextFactory<ApplicationDbContext> _contexts;

    private readonly IEnrollmentService _enrollments;

    private readonly IEventService _events;

    private readonly IPushNotificationFactory _pushFactory;

    private readonly IPushNotificationService _push;

    private readonly IAuditLogService _audit;

    private readonly ILogger<EventAgendaService> _logger;

    private readonly TimeProvider _time;

    public EventAgendaService(
        IDbContextFactory<ApplicationDbContext> contexts,
        IEnrollmentService enrollments,
        IEventService events,
        IPushNotificationFactory pushFactory,
        IPushNotificationService push,
        IAuditLogService audit,
        ILogger<EventAgendaService> logger,
        TimeProvider? time = null)
    {
        _contexts = contexts;
        _enrollments = enrollments;
        _events = events;
        _pushFactory = pushFactory;
        _push = push;
        _audit = audit;
        _logger = logger;
        _time = time ?? TimeProvider.System;
    }

    public async Task<EventAgendaDto> GetAgendaAsync(ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        // ponytail: whole table in memory (57 events today); page it if it ever reaches thousands.
        var events = await VisibleTo(db.Events.AsNoTracking(), user).ToListAsync();
        var extras = await LoadExtrasAsync(db, events.Select(e => e.Id).ToList(), EventsAuthorization.UserId(user));

        var canEdit = EventsAuthorization.CanEdit(user);
        return new EventAgendaDto(
            extras.IsMember,
            EventsAuthorization.CanManage(user),
            Upcoming(events).Select(e => ToSummary(e, extras)).ToList(),
            events.Where(IsPast).OrderByDescending(e => e.Date).ThenByDescending(e => e.Id)
                .Select(e => ToSummary(e, extras)).ToList(),
            canEdit ? TypeOptions : null,
            canEdit);
    }

    public async Task<IReadOnlyList<UpcomingEventDto>> GetUpcomingPreviewAsync(int count)
    {
        // Anonymous and cacheable: public events only, whoever asks.
        await using var db = await _contexts.CreateDbContextAsync();
        // Upcoming = not past (its last day is today or later), soonest first - decided by SQL, so only `count` rows
        // leave the database. The agenda keeps its in-memory Upcoming: the member view needs every event.
        var today = Today;
        var events = await db.Events.AsNoTracking()
            .Where(e => e.IsPublic && (e.EndDate ?? e.Date) >= today)
            .OrderBy(e => e.Date).ThenBy(e => e.Id)
            .Take(count)
            .ToListAsync();
        return events.Select(UpcomingEventDto.From).ToList();
    }

    public async Task<EventDetailDto?> GetEventAsync(int id, ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        // A members-only event is a 404 for a visitor, exactly like one that does not exist.
        var e = await VisibleTo(db.Events.AsNoTracking(), user).FirstOrDefaultAsync(x => x.Id == id);
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
            // The same rows "Quem vai" shows give the not-going count: one query fewer.
            var people = await PeopleAsync(db, id);
            var notGoing = people.Count(p => !p.WillAttend);
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
                Participants(people));
        }

        var canManage = EventsAuthorization.CanManage(user);
        var me = EventsAuthorization.UserId(user);
        // The current Ensaiador / Magister prepare the repertoire of an upcoming, not cancelled event.
        var canPrepare = !canManage && extras.IsMember && !e.IsCancelled && !IsPast(e)
            && (await db.Users.AsNoTracking().Where(u => u.Id == me).Select(u => u.Positions).FirstOrDefaultAsync())
                ?.Any(RehearsalsAuthorization.Approvers.Contains) == true;
        return new EventDetailDto(extras.IsMember, canManage, ToSummary(e, extras), videos, member,
            canManage && EventAdminService.TakesPrizes(e, Today), await EventsAuthorization.CanTrackContactsAsync(db, user),
            canManage || canPrepare);
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

        // The member's other instruments, by display name.
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
}
