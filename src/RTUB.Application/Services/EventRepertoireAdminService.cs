using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// Repertoire management behind the React event page. Rules enforced
/// server-side: Admin or Owner (also the current Ensaiador / Magister for upcoming events); songs go on one of the event's days (or a day that already has
/// songs), appended after the day's last; a song appears once per event (the database's unique
/// EventId+SongId index, checked across every day); a day
/// can be cleared. The song picker shows only what Music would show the caller. No schema change.
/// </summary>
public sealed partial class EventRepertoireAdminService : IEventRepertoireAdminService
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
        if (await RefusalAsync<EventRepertoireManageDto>(id, user) is { } refused)
        {
            return refused;
        }

        var e = await FindAsync(id);
        return e is null ? EventResult<EventRepertoireManageDto>.Fail(EventResultStatus.NotFound) : EventResult<EventRepertoireManageDto>.Ok(await ManageAsync(e));
    }

    public async Task<EventResult<IReadOnlyList<EventRepertoireSongDto>>> SearchSongsAsync(int id, string? query, ClaimsPrincipal user)
    {
        if (await RefusalAsync<IReadOnlyList<EventRepertoireSongDto>>(id, user) is { } refused)
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
        if (await RefusalAsync<EventRepertoireManageDto>(id, user) is { } refused)
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

            // Once per event (the unique index), and no second song of the same title: the picker
            // lists each title once, so two versions of one song never go in together.
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
        if (await RefusalAsync<EventRepertoireManageDto>(id, user) is { } refused)
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
        if (await RefusalAsync<EventRepertoireManageDto>(id, user) is { } refused)
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
        if (await RefusalAsync<EventRepertoireManageDto>(id, user) is { } refused)
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

        // The existing service orders by song id (positions 1..n).
        await _repertoire.UpdateRepertoireOrderAsync(id, day.Value, order.Select(i => songOf[i]).ToList());
        return EventResult<EventRepertoireManageDto>.Ok(await ManageAsync(e));
    }
}
