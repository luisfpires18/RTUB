using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

// "As minhas ações pendentes" (task 071): the Atuações and Ensaios panels. An action here is an activity the member has
// not answered at all (no Enrollment / RehearsalAttendance row of theirs, whatever its answer), not cancelled:
// - "Próximas": every upcoming one (no seven-day or other window);
// - "Passadas": past ones of the current fiscal year (1 Sep - 31 Aug, by the activity's start).
// Upcoming/past is each module's own rule (events: last day before today in Portugal; rehearsals: day before today).
// In 071 a row opens the module's page, where the member answers with that module's rules (instruments, notes,
// presença pending approval); the in-page Vou / Não vou and Fui / Não fui answers are #253. Not dismissible: an
// answer, not a "não", resolves the row. Active members only (docs/permissions/pending-actions.md).

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
}

/// <summary>Atuações por responder (events the member has not answered).</summary>
public sealed class EventResponsePendingActionSource : IPendingActionSource
{
    public const string Kind = "event.response";

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

    public async Task<(PendingActionRowDto Row, string Period)?> FindAsync(ApplicationDbContext db, PendingActionMember member, string kind, string sourceKey)
    {
        if (kind != Kind || !int.TryParse(sourceKey, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
        {
            return null;
        }

        foreach (var (filter, _) in Filters)
        {
            if (await Query(db, member, filter).FirstOrDefaultAsync(e => e.Id == id) is { } e)
            {
                return (Row(e, filter == AttendanceFilters.Past), member.FiscalYear);
            }
        }

        return null;
    }

    private static IQueryable<Event> Query(ApplicationDbContext db, PendingActionMember member, string? filter)
    {
        var today = member.Today;
        var unanswered = db.Events.AsNoTracking()
            .Where(e => !e.IsCancelled && !db.Enrollments.Any(x => x.EventId == e.Id && x.UserId == member.UserId));
        var start = member.FiscalYearStartDate;
        return filter == AttendanceFilters.Past
            ? unanswered.Where(e => (e.EndDate ?? e.Date) < today && e.Date >= start)
            : unanswered.Where(e => (e.EndDate ?? e.Date) >= today);
    }

    private static PendingActionRowDto Row(Event e, bool past) => new(
        Kind,
        e.Id.ToString(CultureInfo.InvariantCulture),
        e.Name,
        AttendanceFilters.Day(e.Date),
        AttendanceFilters.Clock(e.Date.TimeOfDay),
        AttendanceFilters.Join(e.Location, e.Type.GetDisplayName()),
        Required: false,
        Dismissible: false,
        past
            ? [new PendingActionButtonDto("Ver atuação", $"/events/{e.Id}", false)]
            : [new PendingActionButtonDto("Responder", $"/events/{e.Id}?respond=1", true)]);
}

/// <summary>Ensaios por responder (rehearsals the member has not answered).</summary>
public sealed class RehearsalResponsePendingActionSource : IPendingActionSource
{
    public const string Kind = "rehearsal.response";

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

    public async Task<(PendingActionRowDto Row, string Period)?> FindAsync(ApplicationDbContext db, PendingActionMember member, string kind, string sourceKey)
    {
        if (kind != Kind || !int.TryParse(sourceKey, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
        {
            return null;
        }

        foreach (var (filter, _) in Filters)
        {
            if (await Query(db, member, filter).FirstOrDefaultAsync(r => r.Id == id) is { } r)
            {
                return (Row(r, filter == AttendanceFilters.Past), member.FiscalYear);
            }
        }

        return null;
    }

    private static IQueryable<Rehearsal> Query(ApplicationDbContext db, PendingActionMember member, string? filter)
    {
        var today = member.Today;
        var unanswered = db.Rehearsals.AsNoTracking()
            .Where(r => !r.IsCanceled && !db.RehearsalAttendances.Any(a => a.RehearsalId == r.Id && a.UserId == member.UserId));
        var start = member.FiscalYearStartDate;
        return filter == AttendanceFilters.Past
            ? unanswered.Where(r => r.Date < today && r.Date >= start)
            : unanswered.Where(r => r.Date >= today);
    }

    private static PendingActionRowDto Row(Rehearsal r, bool past) => new(
        Kind,
        r.Id.ToString(CultureInfo.InvariantCulture),
        string.IsNullOrWhiteSpace(r.Theme) ? "Ensaio" : $"Ensaio · {r.Theme!.Trim()}",
        AttendanceFilters.Day(r.Date),
        AttendanceFilters.Clock(r.StartTime),
        AttendanceFilters.Join(r.Location),
        Required: false,
        Dismissible: false,
        past
            ? [new PendingActionButtonDto("Ver ensaio", $"/rehearsals/{r.Id}", false)]
            : [new PendingActionButtonDto("Responder", $"/rehearsals/{r.Id}", true)]);
}
