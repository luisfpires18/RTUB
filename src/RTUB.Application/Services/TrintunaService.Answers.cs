// Trintuna: the date vote, Vou / Não vou, and the Owner's access list.
using System.Globalization;
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
    /// <summary>A new vote replaces the caller's earlier one. Closed (409) once the date is set.</summary>
    public async Task<EventResult<GatheringDto>> VoteAsync(int id, GatheringOptionInput input, ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var (refused, listed) = await SeeAsync<GatheringDto>(db, user);
        if (refused is not null)
        {
            return refused;
        }

        var gathering = await db.Gatherings.Include(g => g.PollOptions).FirstOrDefaultAsync(g => g.Id == id);
        if (gathering is null)
        {
            return EventResult<GatheringDto>.Fail(EventResultStatus.NotFound);
        }

        if (PollClosed(gathering))
        {
            return EventResult<GatheringDto>.Fail(EventResultStatus.Closed);
        }

        if (gathering.PollOptions.All(o => o.Id != input.OptionId))
        {
            return EventResult<GatheringDto>.Invalid("optionId", "Escolha uma das datas possíveis deste almoço.");
        }

        var me = user.UserId()!;
        var vote = await db.GatheringPollVotes.FirstOrDefaultAsync(v => v.GatheringId == id && v.UserId == me);
        if (vote is null)
        {
            db.GatheringPollVotes.Add(vote = new GatheringPollVote { GatheringId = id, UserId = me });
        }

        vote.GatheringPollOptionId = input.OptionId;
        vote.VotedAt = _time.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync();
        return EventResult<GatheringDto>.Ok(await LoadAsync(db, id, user, listed));
    }

    /// <summary>Vou / Não vou; Closed (409) for a past gathering.</summary>
    public async Task<EventResult<GatheringDto>> SetPresenceAsync(int id, GatheringPresenceInput input, ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var (refused, listed) = await SeeAsync<GatheringDto>(db, user);
        if (refused is not null)
        {
            return refused;
        }

        if (await OpenForPresenceAsync<GatheringDto>(db, id) is { } closed)
        {
            return closed;
        }

        var me = user.UserId()!;
        var answer = await db.GatheringParticipants.FirstOrDefaultAsync(p => p.GatheringId == id && p.UserId == me);
        if (answer is null)
        {
            db.GatheringParticipants.Add(answer = new GatheringParticipant { GatheringId = id, UserId = me });
        }

        answer.WillAttend = input.WillAttend;
        answer.AnsweredAt = _time.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync();
        return EventResult<GatheringDto>.Ok(await LoadAsync(db, id, user, listed));
    }

    public async Task<EventResult<bool>> ClearPresenceAsync(int id, ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var (refused, _) = await SeeAsync<bool>(db, user);
        if (refused is not null)
        {
            return refused;
        }

        if (await OpenForPresenceAsync<bool>(db, id) is { } closed)
        {
            return closed;
        }

        var me = user.UserId()!;
        await db.GatheringParticipants.Where(p => p.GatheringId == id && p.UserId == me).ExecuteDeleteAsync();
        return EventResult<bool>.Ok(true);
    }

    /// <summary>NotFound for an unknown gathering, Closed for a past one; null when answers are taken.</summary>
    private async Task<EventResult<T>?> OpenForPresenceAsync<T>(ApplicationDbContext db, int id)
    {
        var gathering = await db.Gatherings.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id);
        return gathering is null ? EventResult<T>.Fail(EventResultStatus.NotFound)
            : IsPast(gathering, PortugalTime.Today(_time)) ? EventResult<T>.Fail(EventResultStatus.Closed)
            : null;
    }

    public async Task<EventResult<IReadOnlyList<TrintunaMemberDto>>> GetAccessAsync(ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        if (await OwnerRefusalAsync<IReadOnlyList<TrintunaMemberDto>>(db, user) is { } refused)
        {
            return refused;
        }

        return EventResult<IReadOnlyList<TrintunaMemberDto>>.Ok(await AccessListAsync(db));
    }

    /// <summary>Replaces the list (rows kept, added or removed as needed).</summary>
    public async Task<EventResult<IReadOnlyList<TrintunaMemberDto>>> UpdateAccessAsync(TrintunaAccessInput input, ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        if (await OwnerRefusalAsync<IReadOnlyList<TrintunaMemberDto>>(db, user) is { } refused)
        {
            return refused;
        }

        var ids = (input.UserIds ?? Array.Empty<string>()).Where(i => !string.IsNullOrWhiteSpace(i)).Distinct().ToList();
        var known = await db.Users.Where(u => ids.Contains(u.Id) && !u.IsExpelled).CountAsync();
        if (known != ids.Count)
        {
            return EventResult<IReadOnlyList<TrintunaMemberDto>>.Invalid("userIds", "Há membros na lista que já não existem.");
        }

        var rows = await db.TrintunaAccesses.ToListAsync();
        db.TrintunaAccesses.RemoveRange(rows.Where(r => !ids.Contains(r.UserId)));
        foreach (var id in ids.Where(i => rows.All(r => r.UserId != i)))
        {
            db.TrintunaAccesses.Add(new TrintunaAccess { UserId = id, AddedAt = _time.GetUtcNow().UtcDateTime });
        }

        await db.SaveChangesAsync();
        return EventResult<IReadOnlyList<TrintunaMemberDto>>.Ok(await AccessListAsync(db));
    }

    /// <summary>The access list is Owner only: 401 visitor, 404 member who cannot see the page, 403 listed member.</summary>
    private static async Task<EventResult<T>?> OwnerRefusalAsync<T>(ApplicationDbContext db, ClaimsPrincipal user)
    {
        var (refused, _) = await SeeAsync<T>(db, user);
        return refused ?? (TrintunaAuthorization.CanManage(user) ? null : EventResult<T>.Fail(EventResultStatus.Forbidden));
    }

    private static async Task<IReadOnlyList<TrintunaMemberDto>> AccessListAsync(ApplicationDbContext db)
    {
        var users = await db.TrintunaAccesses.AsNoTracking()
            .Select(a => new { a.User!.Id, a.User.Nickname, a.User.FirstName, a.User.LastName, a.User.ImageUrl })
            .ToListAsync();
        return users
            .Select(u => (u.Id, who: GovernanceService.ToMember(u.Nickname, u.FirstName, u.LastName, u.ImageUrl)))
            .OrderBy(x => x.who.DisplayName, StringComparer.Create(CultureInfo.GetCultureInfo("pt-PT"), true))
            .Select(x => new TrintunaMemberDto(x.Id, x.who.DisplayName, x.who.AvatarUrl))
            .ToList();
    }
}
