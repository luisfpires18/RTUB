using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

// "As minhas ações pendentes" (tasks 071 / 072): the Atuações and Ensaios panels. An action here is an activity the member
// has not answered at all (no Enrollment / RehearsalAttendance row of theirs, whatever its answer), not cancelled:
// - "Próximas": every upcoming one (no seven-day or other window), answered Vou / Não vou;
// - "Passadas": past ones from PendingActionMember.PastFrom (1 September of the current fiscal year, or the member's
//   admission month when later), by the activity's start, answered Fui / Não fui.
// Upcoming/past is each module's own rule (events: last day before today in Portugal; rehearsals: day before today), so
// an event in progress or later today is still "Próximas", as on its page. Answers are written through the module's
// domain service, create-only (docs/react-pending-actions.md → Answers). Not dismissible: an answer, not a "não",
// resolves the row. Active members only (docs/react-pending-actions.md).

internal static class AttendanceFilters
{
    public const string Upcoming = "upcoming";
    public const string Past = "past";

    public static readonly IReadOnlyList<(string Key, string Label)> All = [(Upcoming, "Próximas"), (Past, "Passadas")];

    public static string Day(DateTime date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string? Clock(TimeSpan time) => time == TimeSpan.Zero ? null : time.ToString(@"hh\:mm", CultureInfo.InvariantCulture);

    public static string? Join(params string?[] parts)
    {
        var text = string.Join(" · ", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));
        return text.Length == 0 ? null : text;
    }

    public static IReadOnlyList<PendingActionButtonDto> Answers(bool past) => past
        ? [new PendingActionButtonDto("Fui", null, true, "yes"), new PendingActionButtonDto("Não fui", null, false, "no")]
        : [new PendingActionButtonDto("Vou", null, true, "yes"), new PendingActionButtonDto("Não vou", null, false, "no")];

    public static int? Id(string sourceKey) =>
        int.TryParse(sourceKey, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0 ? id : null;

    /// <summary>A first answer that lost a race with another one (the unique index) or found one already there.</summary>
    public static bool IsAlreadyAnswered(Exception ex) => ex is DbUpdateException or InvalidOperationException;
}

/// <summary>Atuações por responder (events the member has not answered).</summary>
public sealed class EventResponsePendingActionSource : IPendingActionSource, IPendingActionResponder
{
    public const string KindCode = "event.response";

    private readonly IEnrollmentService _enrollments;
    private readonly IMemberStatusService _status;

    public EventResponsePendingActionSource(IEnrollmentService enrollments, IMemberStatusService status)
    {
        _enrollments = enrollments;
        _status = status;
    }

    public string Kind => KindCode;
    public int NotesMax => EventAgendaService.MaxNotesLength;
    public string Panel => "events";
    public string Title => "Atuações";
    public string Category => "participation";
    public IReadOnlyList<(string Key, string Label)> Filters => AttendanceFilters.All;

    public bool AppliesTo(PendingActionMember member) => member.IsActive;

    public Task<int> CountAsync(ApplicationDbContext db, PendingActionMember member, string? filter) =>
        Query(db, member, filter).CountAsync();

    public async Task<IReadOnlyList<PendingActionRowDto>> PageAsync(ApplicationDbContext db, PendingActionMember member, string? filter, int skip, int take)
    {
        var query = Query(db, member, filter);
        query = filter == AttendanceFilters.Past
            ? query.OrderByDescending(e => e.Date).ThenByDescending(e => e.Id)
            : query.OrderBy(e => e.Date).ThenBy(e => e.Id);
        var events = await query.Skip(skip).Take(take).ToListAsync();
        return events.Select(e => Row(e, filter == AttendanceFilters.Past)).ToList();
    }

    public async Task<(PendingActionRowDto Row, string Period)?> FindAsync(ApplicationDbContext db, PendingActionMember member, string kind, string sourceKey) =>
        kind == Kind && AttendanceFilters.Id(sourceKey) is { } id && await PendingAsync(db, member, id) is { } found
            ? (Row(found.Event, found.Past), member.FiscalYear)
            : null;

    /// <summary>
    /// Vou / Não vou or Fui / Não fui: the member's own first Enrollment, through <see cref="IEnrollmentService"/> (category
    /// snapshot; the attendees' push for an upcoming event, none for a past one, as the service sends pushes for future
    /// events only). "Fui" is a past Enrollment with WillAttend, the same row an Admin adds in "Quem foi": it counts as a
    /// performance for activity status, so the member's status is reconciled at once, as a presença approval does.
    /// </summary>
    public async Task<EventResult<bool>> RespondAsync(ApplicationDbContext db, PendingActionMember member, string sourceKey, bool yes,
        InstrumentType? instrument, string? otherInstruments, string? notes)
    {
        if (AttendanceFilters.Id(sourceKey) is not { } id || await PendingAsync(db, member, id) is not { } found)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        try
        {
            await _enrollments.CreateEnrollmentAsync(member.UserId, id, instrument, notes, yes, otherInstruments, skipNotification: found.Past);
        }
        catch (Exception ex) when (AttendanceFilters.IsAlreadyAnswered(ex))
        {
            return EventResult<bool>.Fail(EventResultStatus.Conflict);
        }

        if (yes && found.Past)
        {
            await _status.ReconcileMemberStatusAsync(member.UserId);
        }

        return EventResult<bool>.Ok(true);
    }

    private static async Task<(Event Event, bool Past)?> PendingAsync(ApplicationDbContext db, PendingActionMember member, int id)
    {
        foreach (var (filter, _) in AttendanceFilters.All)
        {
            if (await Query(db, member, filter).FirstOrDefaultAsync(e => e.Id == id) is { } e)
            {
                return (e, filter == AttendanceFilters.Past);
            }
        }

        return null;
    }

    private static IQueryable<Event> Query(ApplicationDbContext db, PendingActionMember member, string? filter)
    {
        var today = member.Today;
        var unanswered = db.Events.AsNoTracking()
            .Where(e => !e.IsCancelled && !db.Enrollments.Any(x => x.EventId == e.Id && x.UserId == member.UserId));
        var start = member.PastFrom;
        return filter == AttendanceFilters.Past
            ? unanswered.Where(e => (e.EndDate ?? e.Date) < today && e.Date >= start)
            : unanswered.Where(e => (e.EndDate ?? e.Date) >= today);
    }

    private static PendingActionRowDto Row(Event e, bool past) => new(
        KindCode,
        e.Id.ToString(CultureInfo.InvariantCulture),
        e.Name,
        AttendanceFilters.Day(e.Date),
        AttendanceFilters.Clock(e.Date.TimeOfDay),
        AttendanceFilters.Join(e.Location, e.Type.GetDisplayName()),
        Required: false,
        Dismissible: false,
        AttendanceFilters.Answers(past),
        $"/events/{e.Id}");
}

/// <summary>Ensaios por responder (rehearsals the member has not answered).</summary>
public sealed class RehearsalResponsePendingActionSource : IPendingActionSource, IPendingActionResponder
{
    public const string KindCode = "rehearsal.response";

    private readonly IRehearsalAttendanceService _attendance;

    public RehearsalResponsePendingActionSource(IRehearsalAttendanceService attendance)
    {
        _attendance = attendance;
    }

    public string Kind => KindCode;
    public int NotesMax => RehearsalAgendaService.NotesMax;
    public string Panel => "rehearsals";
    public string Title => "Ensaios";
    public string Category => "participation";
    public IReadOnlyList<(string Key, string Label)> Filters => AttendanceFilters.All;

    public bool AppliesTo(PendingActionMember member) => member.IsActive;

    public Task<int> CountAsync(ApplicationDbContext db, PendingActionMember member, string? filter) =>
        Query(db, member, filter).CountAsync();

    public async Task<IReadOnlyList<PendingActionRowDto>> PageAsync(ApplicationDbContext db, PendingActionMember member, string? filter, int skip, int take)
    {
        var query = Query(db, member, filter);
        query = filter == AttendanceFilters.Past
            ? query.OrderByDescending(r => r.Date).ThenByDescending(r => r.Id)
            : query.OrderBy(r => r.Date).ThenBy(r => r.Id);
        var rehearsals = await query.Skip(skip).Take(take).ToListAsync();
        return rehearsals.Select(r => Row(r, filter == AttendanceFilters.Past)).ToList();
    }

    public async Task<(PendingActionRowDto Row, string Period)?> FindAsync(ApplicationDbContext db, PendingActionMember member, string kind, string sourceKey) =>
        kind == Kind && AttendanceFilters.Id(sourceKey) is { } id && await PendingAsync(db, member, id) is { } found
            ? (Row(found.Rehearsal, found.Past), member.FiscalYear)
            : null;

    /// <summary>
    /// Vou / Não vou or Fui / Não fui: the member's own first presença through <see cref="IRehearsalAttendanceService"/>,
    /// always pending (Attended = false) - a "Fui" is a self-report an Admin still approves, so it earns nothing (no XP,
    /// no activity) until then. Never creates a rehearsal; never touches an existing presença.
    /// </summary>
    public async Task<EventResult<bool>> RespondAsync(ApplicationDbContext db, PendingActionMember member, string sourceKey, bool yes,
        InstrumentType? instrument, string? otherInstruments, string? notes)
    {
        if (AttendanceFilters.Id(sourceKey) is not { } id || await PendingAsync(db, member, id) is not { } found)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        try
        {
            await _attendance.AddFirstAttendanceAsync(id, member.UserId, yes, instrument, notes, otherInstruments, skipNotification: found.Past);
        }
        catch (Exception ex) when (AttendanceFilters.IsAlreadyAnswered(ex))
        {
            return EventResult<bool>.Fail(EventResultStatus.Conflict);
        }

        return EventResult<bool>.Ok(true);
    }

    private static async Task<(Rehearsal Rehearsal, bool Past)?> PendingAsync(ApplicationDbContext db, PendingActionMember member, int id)
    {
        foreach (var (filter, _) in AttendanceFilters.All)
        {
            if (await Query(db, member, filter).FirstOrDefaultAsync(r => r.Id == id) is { } r)
            {
                return (r, filter == AttendanceFilters.Past);
            }
        }

        return null;
    }

    private static IQueryable<Rehearsal> Query(ApplicationDbContext db, PendingActionMember member, string? filter)
    {
        var today = member.Today;
        var unanswered = db.Rehearsals.AsNoTracking()
            .Where(r => !r.IsCanceled && !db.RehearsalAttendances.Any(a => a.RehearsalId == r.Id && a.UserId == member.UserId));
        var start = member.PastFrom;
        return filter == AttendanceFilters.Past
            ? unanswered.Where(r => r.Date < today && r.Date >= start)
            : unanswered.Where(r => r.Date >= today);
    }

    private static PendingActionRowDto Row(Rehearsal r, bool past) => new(
        KindCode,
        r.Id.ToString(CultureInfo.InvariantCulture),
        string.IsNullOrWhiteSpace(r.Theme) ? "Ensaio" : $"Ensaio · {r.Theme!.Trim()}",
        AttendanceFilters.Day(r.Date),
        AttendanceFilters.Clock(r.StartTime),
        AttendanceFilters.Join(r.Location),
        Required: false,
        Dismissible: false,
        AttendanceFilters.Answers(past),
        $"/rehearsals/{r.Id}");
}
