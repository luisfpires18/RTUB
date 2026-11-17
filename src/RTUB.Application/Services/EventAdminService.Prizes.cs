// Event admin: prizes (012B).
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.DTOs;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

public sealed partial class EventAdminService
{
    // ---------- prizes (012B) ----------

    public const int MaxPrizeNameLength = 200;

    /// <summary>
    /// Where a prize can be added: a festival whose last day has passed. The old page only offered the
    /// prizes button on past festivals; existing prizes stay editable and deletable on any event.
    /// </summary>
    public static bool TakesPrizes(Event e, DateTime today) => e.Type == EventType.Festival && IsPast(e, today);

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

        if (!TakesPrizes(e, Today))
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
}
