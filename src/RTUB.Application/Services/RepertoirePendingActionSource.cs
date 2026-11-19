using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

// Pending action: event repertoire to prepare.

/// <summary>
/// Repertório (current Ensaiador / Magister): upcoming, not cancelled events with no song in their repertoire.
/// "Preparar repertório" opens the event's repertoire editor (the same editor; the Ensaiador / Magister may use it for
/// upcoming events). "Não é necessário" is the member's own dismissal of that event; a repertoire saved by
/// anyone resolves it for everyone.
/// </summary>
public sealed class RepertoirePendingActionSource : IPendingActionSource
{
    public const string KindCode = "repertoire.event";

    public string Panel => "repertoire";
    public string Title => "Repertório";
    public string Category => "responsibilities";
    public IReadOnlyList<(string Key, string Label)> Filters => [];

    public bool AppliesTo(PendingActionMember member) => member.Holds(RehearsalsAuthorization.Approvers);

    public Task<int> CountAsync(ApplicationDbContext db, PendingActionMember member, string? filter) => Query(db, member).CountAsync();

    public async Task<IReadOnlyList<PendingActionRowDto>> PageAsync(ApplicationDbContext db, PendingActionMember member, string? filter, int skip, int take)
    {
        var events = await Query(db, member).OrderBy(e => e.Date).ThenBy(e => e.Id).Skip(skip).Take(take)
            .Select(e => new { e.Id, e.Name, e.Date, e.Location }).ToListAsync();
        return events.Select(e => Row(e.Id, e.Name, e.Date, e.Location)).ToList();
    }

    public async Task<(PendingActionRowDto Row, string Period)?> FindAsync(ApplicationDbContext db, PendingActionMember member, string kind, string sourceKey) =>
        kind == KindCode && AttendanceFilters.Id(sourceKey) is { } id
            && await Query(db, member).Where(e => e.Id == id).Select(e => new { e.Id, e.Name, e.Date, e.Location }).FirstOrDefaultAsync() is { } ev
            ? (Row(ev.Id, ev.Name, ev.Date, ev.Location), "")
            : null;

    private static IQueryable<Event> Query(ApplicationDbContext db, PendingActionMember member)
    {
        var today = member.Today;
        var dismissed = db.PendingActionDismissals.Where(d => d.UserId == member.UserId && d.ActionKind == KindCode && d.Period == "")
            .Select(d => d.SourceKey);
        return db.Events.AsNoTracking()
            .Where(e => !e.IsCancelled && (e.EndDate ?? e.Date) >= today && !db.EventRepertoires.Any(r => r.EventId == e.Id))
            .Where(e => !dismissed.Contains(e.Id.ToString()));
    }

    private static PendingActionRowDto Row(int id, string name, DateTime date, string? location) => new(
        KindCode, id.ToString(CultureInfo.InvariantCulture), name, AttendanceFilters.Day(date), AttendanceFilters.Clock(date.TimeOfDay),
        AttendanceFilters.Join(location, "Sem repertório"),
        Required: false, Dismissible: true,
        [new PendingActionButtonDto("Preparar repertório", $"/events/{id}?repertoire=1", true)],
        $"/events/{id}",
        DismissLabel: "Não é necessário");
}
