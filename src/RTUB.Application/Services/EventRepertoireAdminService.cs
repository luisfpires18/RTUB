using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// The old /member/events repertoire modal behind the React event page (React track 012D). Same rules,
/// now server-side: Admin or Owner only; songs go on one of the event's days (or a day that already has
/// songs), appended after the day's last; a song appears once per event (the database's unique
/// EventId+SongId index; the old modal only checked the day and failed silently on another day); a day
/// can be cleared. The song picker shows only what Music would show the caller. No schema change.
/// </summary>
public sealed class EventRepertoireAdminService : IEventRepertoireAdminService
{
    public const int MaxDisplayOrder = 1000;
    public const int SearchLimit = 20;

    private readonly IDbContextFactory<ApplicationDbContext> _contexts;
    private readonly IEventRepertoireService _repertoire;

    public EventRepertoireAdminService(IDbContextFactory<ApplicationDbContext> contexts, IEventRepertoireService repertoire)
    {
        _contexts = contexts;
        _repertoire = repertoire;
    }

    public async Task<EventResult<EventRepertoireManageDto>> GetAsync(int id, ClaimsPrincipal user)
    {
        if (Refusal<EventRepertoireManageDto>(user) is { } refused)
        {
            return refused;
        }

        var e = await FindAsync(id);
        return e is null ? EventResult<EventRepertoireManageDto>.Fail(EventResultStatus.NotFound) : EventResult<EventRepertoireManageDto>.Ok(await ManageAsync(e));
    }

    public async Task<EventResult<IReadOnlyList<EventRepertoireSongDto>>> SearchSongsAsync(int id, string? query, ClaimsPrincipal user)
    {
        if (Refusal<IReadOnlyList<EventRepertoireSongDto>>(user) is { } refused)
        {
            return refused;
        }

        if (await FindAsync(id) is null)
        {
            return EventResult<IReadOnlyList<EventRepertoireSongDto>>.Fail(EventResultStatus.NotFound);
        }

        await using var db = await _contexts.CreateDbContextAsync();
        var taken = await TakenTitlesAsync(db, id);
        var songs = await VisibleSongs(db, user)
            .Select(s => new { s.Id, s.Title, Album = s.Album!.Title })
            .ToListAsync();

        // ponytail: filtered in memory (a few hundred songs) so accents and case fold the Portuguese way.
        var q = Fold(query);
        return EventResult<IReadOnlyList<EventRepertoireSongDto>>.Ok(songs
            .Where(s => !taken.Contains(Fold(s.Title)))
            .Where(s => q.Length == 0 || Fold(s.Title).Contains(q))
            .OrderBy(s => Fold(s.Title).StartsWith(q) ? 0 : 1)
            .ThenBy(s => s.Title, StringComparer.Create(CultureInfo.GetCultureInfo("pt-PT"), true))
            .ThenBy(s => s.Id)
            .Take(SearchLimit)
            .Select(s => new EventRepertoireSongDto(s.Id, s.Title, s.Album))
            .ToList());
    }

    public async Task<EventResult<EventRepertoireManageDto>> AddAsync(int id, EventRepertoireAddInput input, ClaimsPrincipal user)
    {
        if (Refusal<EventRepertoireManageDto>(user) is { } refused)
        {
            return refused;
        }

        var e = await FindAsync(id);
        if (e is null)
        {
            return EventResult<EventRepertoireManageDto>.Fail(EventResultStatus.NotFound);
        }

        var day = await DayOfAsync(e, input.Date);
        if (day is null)
        {
            return EventResult<EventRepertoireManageDto>.Invalid("date", "Escolha um dos dias da atuação.");
        }

        await using (var db = await _contexts.CreateDbContextAsync())
        {
            var title = await VisibleSongs(db, user).Where(s => s.Id == input.SongId).Select(s => s.Title).FirstOrDefaultAsync();
            if (title is null)
            {
                return EventResult<EventRepertoireManageDto>.Invalid("songId", "Escolha uma música da lista.");
            }

            // Once per event (the unique index), and no second song of the same title: the old picker
            // listed each title once, so two versions of one song never went in together.
            if (await db.EventRepertoires.AnyAsync(r => r.EventId == id && r.SongId == input.SongId)
                || (await TakenTitlesAsync(db, id)).Contains(Fold(title)))
            {
                return EventResult<EventRepertoireManageDto>.Invalid("songId", "Esta música já está no repertório desta atuação.");
            }

            var last = await db.EventRepertoires.Where(r => r.EventId == id && r.RepertoireDate.Date == day.Value)
                .MaxAsync(r => (int?)r.DisplayOrder) ?? 0;
            if (last >= MaxDisplayOrder)
            {
                return EventResult<EventRepertoireManageDto>.Invalid("songId", $"Um dia não leva mais de {MaxDisplayOrder} músicas.");
            }

            await _repertoire.AddSongToRepertoireAsync(id, input.SongId, last + 1, day.Value);
        }

        return EventResult<EventRepertoireManageDto>.Ok(await ManageAsync(e));
    }

    public async Task<EventResult<EventRepertoireManageDto>> RemoveAsync(int id, int itemId, ClaimsPrincipal user)
    {
        if (Refusal<EventRepertoireManageDto>(user) is { } refused)
        {
            return refused;
        }

        var e = await FindAsync(id);
        await using (var db = await _contexts.CreateDbContextAsync())
        {
            if (e is null || !await db.EventRepertoires.AnyAsync(r => r.Id == itemId && r.EventId == id))
            {
                return EventResult<EventRepertoireManageDto>.Fail(EventResultStatus.NotFound);
            }
        }

        await _repertoire.RemoveSongFromRepertoireAsync(itemId);
        return EventResult<EventRepertoireManageDto>.Ok(await ManageAsync(e));
    }

    public async Task<EventResult<EventRepertoireManageDto>> RemoveDayAsync(int id, string? date, ClaimsPrincipal user)
    {
        if (Refusal<EventRepertoireManageDto>(user) is { } refused)
        {
            return refused;
        }

        var e = await FindAsync(id);
        if (e is null)
        {
            return EventResult<EventRepertoireManageDto>.Fail(EventResultStatus.NotFound);
        }

        var day = await DayOfAsync(e, date);
        if (day is null)
        {
            return EventResult<EventRepertoireManageDto>.Invalid("date", "Escolha um dos dias da atuação.");
        }

        await _repertoire.RemoveRepertoireDayAsync(id, day.Value);
        return EventResult<EventRepertoireManageDto>.Ok(await ManageAsync(e));
    }

    public async Task<EventResult<EventRepertoireManageDto>> ReorderAsync(int id, EventRepertoireOrderInput input, ClaimsPrincipal user)
    {
        if (Refusal<EventRepertoireManageDto>(user) is { } refused)
        {
            return refused;
        }

        var e = await FindAsync(id);
        if (e is null)
        {
            return EventResult<EventRepertoireManageDto>.Fail(EventResultStatus.NotFound);
        }

        var day = await DayOfAsync(e, input.Date);
        if (day is null)
        {
            return EventResult<EventRepertoireManageDto>.Invalid("date", "Escolha um dos dias da atuação.");
        }

        Dictionary<int, int> songOf;
        await using (var db = await _contexts.CreateDbContextAsync())
        {
            songOf = await db.EventRepertoires.Where(r => r.EventId == id && r.RepertoireDate.Date == day.Value)
                .ToDictionaryAsync(r => r.Id, r => r.SongId);
        }

        // The day's whole list, each row once: a stale or partial order is refused rather than half applied.
        var order = input.ItemIds ?? Array.Empty<int>();
        if (order.Count != songOf.Count || order.Distinct().Count() != order.Count || !order.All(songOf.ContainsKey))
        {
            return EventResult<EventRepertoireManageDto>.Invalid("itemIds", "O repertório mudou entretanto. Recarregue e tente outra vez.");
        }

        // The existing service orders by song id (positions 1..n), as the old drag-and-drop did.
        await _repertoire.UpdateRepertoireOrderAsync(id, day.Value, order.Select(i => songOf[i]).ToList());
        return EventResult<EventRepertoireManageDto>.Ok(await ManageAsync(e));
    }

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

        var userId = MusicAuthorization.UserId(user);
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

    private static EventResult<T>? Refusal<T>(ClaimsPrincipal user) =>
        !EventsAuthorization.IsMember(user) ? EventResult<T>.Fail(EventResultStatus.SignInRequired)
        : !EventsAuthorization.CanManage(user) ? EventResult<T>.Fail(EventResultStatus.Forbidden)
        : null;
}
