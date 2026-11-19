// Event repertoire admin: shared helpers (days, lookups, refusals).
using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

public sealed partial class EventRepertoireAdminService
{
    // ---------- helpers ----------

    /// <summary>The event's days (first to last) plus any other day that already has songs, oldest first.</summary>
    private async Task<List<DateTime>> DaysAsync(Event e)
    {
        var days = new SortedSet<DateTime>();
        var last = (e.EndDate ?? e.Date).Date;
        for (var d = e.Date.Date; d <= last && days.Count < 31; d = d.AddDays(1))
        {
            days.Add(d);
        }

        await using var db = await _contexts.CreateDbContextAsync();
        foreach (var d in await db.EventRepertoires.Where(r => r.EventId == e.Id).Select(r => r.RepertoireDate).ToListAsync())
        {
            days.Add(d.Date);
        }

        return days.ToList();
    }

    private async Task<DateTime?> DayOfAsync(Event e, string? text) =>
        DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) && (await DaysAsync(e)).Contains(d)
            ? d
            : null;

    private async Task<EventRepertoireManageDto> ManageAsync(Event e)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var rows = await db.EventRepertoires.AsNoTracking().Where(r => r.EventId == e.Id)
            .Select(r => new { r.Id, r.RepertoireDate, r.DisplayOrder, Title = r.Song!.Title })
            .ToListAsync();
        var days = await DaysAsync(e);
        return new EventRepertoireManageDto(days
            .Select(d => new EventRepertoireManageDayDto(
                d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                rows.Where(r => r.RepertoireDate.Date == d).OrderBy(r => r.DisplayOrder).ThenBy(r => r.Id)
                    .Select(r => new EventRepertoireItemDto(r.Id, r.Title)).ToList()))
            .ToList());
    }

    /// <summary>
    /// Music's album rule (MusicService.Visible): the Owner sees every song; anyone else every song
    /// except those in exclusive albums they are not listed on.
    /// </summary>
    private static IQueryable<Song> VisibleSongs(ApplicationDbContext db, ClaimsPrincipal user)
    {
        var songs = db.Songs.AsNoTracking();
        if (MusicAuthorization.IsOwner(user))
        {
            return songs;
        }

        var userId = user.UserId();
        return songs.Where(s => !s.Album!.IsExclusive || s.Album.AlbumAccesses.Any(x => x.UserId == userId));
    }

    /// <summary>The titles already in the event, folded (case and accents ignored).</summary>
    private static async Task<HashSet<string>> TakenTitlesAsync(ApplicationDbContext db, int id) =>
        (await db.EventRepertoires.Where(r => r.EventId == id).Select(r => r.Song!.Title).ToListAsync()).Select(Fold).ToHashSet();

    private async Task<Event?> FindAsync(int id)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        return await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
    }

    private static string Fold(string? text) =>
        new string((text ?? string.Empty).Trim().Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray())
            .ToLowerInvariant();

    /// <summary>
    /// Admin / Owner: any event. The current Ensaiador or Magister (positions read from the database) the
    /// repertoire of an upcoming, not cancelled event - only the repertoire, no other event management.
    /// </summary>
    private async Task<EventResult<T>?> RefusalAsync<T>(int eventId, ClaimsPrincipal user)
    {
        if (!user.IsMember())
        {
            return EventResult<T>.Fail(EventResultStatus.SignInRequired);
        }

        return EventsAuthorization.CanManage(user) || await CanPrepareAsync(eventId, user) ? null : EventResult<T>.Fail(EventResultStatus.Forbidden);
    }

    /// <summary>An Ensaiador / Magister and an upcoming (last day today or later, Portugal), not cancelled event.</summary>
    internal async Task<bool> CanPrepareAsync(int eventId, ClaimsPrincipal user)
    {
        if (user.UserId() is not { } me)
        {
            return false;
        }

        await using var db = await _contexts.CreateDbContextAsync();
        var positions = await db.Users.AsNoTracking().Where(u => u.Id == me).Select(u => u.Positions).FirstOrDefaultAsync();
        if (positions?.Any(RehearsalsAuthorization.Approvers.Contains) != true)
        {
            return false;
        }

        var today = RTUB.Core.Helpers.PortugalTime.Today(TimeProvider.System);
        return await db.Events.AsNoTracking().AnyAsync(e => e.Id == eventId && !e.IsCancelled && (e.EndDate ?? e.Date) >= today);
    }
}
