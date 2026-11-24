// Trintuna: the Owner or the organizer marks who went (Fui) on a past gathering, by the page's row key.
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

public sealed partial class TrintunaService
{
    /// <summary>
    /// Fui for a person who can see the page (<see cref="Viewers"/>), found by <paramref name="key"/>. The mark is an
    /// ordinary answer row, so a "Não fui" becomes Fui and the member can still change it.
    /// </summary>
    public async Task<EventResult<GatheringDto>> MarkAttendeeAsync(int id, string key, ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var (refused, listed) = await SeeAsync<GatheringDto>(db, user);
        if (refused is not null)
        {
            return refused;
        }

        if (await MarkRefusalAsync<GatheringDto>(db, id, user, listed) is { } stop)
        {
            return stop;
        }

        var viewerIds = await Viewers(db).Select(u => u.Id).ToListAsync();
        var person = viewerIds.FirstOrDefault(v => RowKey(v) == key);
        if (person is null)
        {
            return EventResult<GatheringDto>.Invalid("key", "Escolha alguém que vê a página da Trintuna.");
        }

        var answer = await db.GatheringParticipants.FirstOrDefaultAsync(p => p.GatheringId == id && p.UserId == person);
        if (answer is null)
        {
            db.GatheringParticipants.Add(answer = new GatheringParticipant { GatheringId = id, UserId = person });
        }

        answer.WillAttend = true;
        answer.AnsweredAt = _time.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync();
        return EventResult<GatheringDto>.Ok(await LoadAsync(db, id, user, listed));
    }

    /// <summary>Removes that person's Fui (never a "Não fui"); nothing to remove is still Ok, as DELETE /presence.</summary>
    public async Task<EventResult<bool>> UnmarkAttendeeAsync(int id, string key, ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var (refused, listed) = await SeeAsync<bool>(db, user);
        if (refused is not null)
        {
            return refused;
        }

        if (await MarkRefusalAsync<bool>(db, id, user, listed) is { } stop)
        {
            return stop;
        }

        // Not limited to viewers: a Fui of someone taken off the list since can still be cleared.
        var went = await db.GatheringParticipants.Where(p => p.GatheringId == id && p.WillAttend).ToListAsync();
        var row = went.FirstOrDefault(p => RowKey(p.UserId) == key);
        if (row is not null)
        {
            db.GatheringParticipants.Remove(row);
            await db.SaveChangesAsync();
        }

        return EventResult<bool>.Ok(true);
    }

    /// <summary>NotFound for an unknown gathering, Forbidden unless Owner or its organizer, Closed unless past.</summary>
    private async Task<EventResult<T>?> MarkRefusalAsync<T>(ApplicationDbContext db, int id, ClaimsPrincipal user, bool listed)
    {
        var gathering = await db.Gatherings.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id);
        if (gathering is null)
        {
            return EventResult<T>.Fail(EventResultStatus.NotFound);
        }

        if (!TrintunaAuthorization.CanEdit(user, listed, gathering.OrganizerUserId))
        {
            return EventResult<T>.Fail(EventResultStatus.Forbidden);
        }

        return IsPast(gathering, PortugalTime.Today(_time)) ? null : EventResult<T>.Fail(EventResultStatus.Closed);
    }
}
