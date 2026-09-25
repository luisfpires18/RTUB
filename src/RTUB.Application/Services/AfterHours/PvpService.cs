using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces.AfterHours;
using RTUB.Core.Entities.AfterHours;
using RTUB.Core.Enums.AfterHours;
using RTUB.Core.Helpers.AfterHours;

namespace RTUB.Application.Services.AfterHours;

public class PvpService(IDbContextFactory<ApplicationDbContext> contextFactory, TimeProvider clock) : IPvpService
{
    public async Task<IReadOnlyList<PvpTargetView>> GetTargetsAsync(string userId)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        await using var context = await contextFactory.CreateDbContextAsync();

        var cycle = await context.AfterHoursGameCycles.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Status == GameCycleStatus.Active);
        if (cycle is null || !cycle.IsPlayableAt(now))
            return [];

        var states = await context.AfterHoursPlayerCycleStates.AsNoTracking()
            .Include(s => s.Gear)
            .Where(s => s.GameCycleId == cycle.Id)
            .ToListAsync();
        var me = states.SingleOrDefault(s => s.UserId == userId);
        if (me is null)
            return [];

        var lastAttacks = await context.AfterHoursPvpBattles.AsNoTracking()
            .Where(b => b.AttackerStateId == me.Id)
            .GroupBy(b => b.DefenderStateId)
            .Select(g => new { DefenderStateId = g.Key, Last = g.Max(b => b.AcceptedAtUtc) })
            .ToDictionaryAsync(x => x.DefenderStateId, x => (DateTime?)x.Last);

        var tuning = await AfterHoursTuningService.LoadAsync(context);
        var names = await NamesAsync(context, states.Select(s => s.UserId));
        return states
            .Where(s => s.Id != me.Id)
            .Select(s => new PvpTargetView(
                s.Id,
                names.GetValueOrDefault(s.UserId, "Unknown"),
                s.Level,
                PvpRules.EffectivePower(s),
                PvpRules.TargetBlockReason(me, s, lastAttacks.GetValueOrDefault(s.Id), now, tuning)))
            .OrderBy(t => t.BlockReason is not null)
            .ThenBy(t => t.DisplayName)
            .ToList();
    }

    public async Task<PvpBattleView?> GetBattleAsync(string userId, int battleId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var battle = await context.AfterHoursPvpBattles.AsNoTracking()
            .Include(b => b.Rounds.OrderBy(r => r.Round))
            .Include(b => b.Cargo)
            .SingleOrDefaultAsync(b => b.Id == battleId && (b.AttackerUserId == userId || b.DefenderUserId == userId));
        if (battle is null)
            return null;

        var names = await NamesAsync(context, [battle.AttackerUserId, battle.DefenderUserId]);
        return new PvpBattleView(battle, names.GetValueOrDefault(battle.AttackerUserId, "Unknown"), names.GetValueOrDefault(battle.DefenderUserId, "Unknown"));
    }

    public async Task<IReadOnlyList<PvpBattleView>> GetRecentBattlesAsync(string userId, int take = 10)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var battles = await context.AfterHoursPvpBattles.AsNoTracking()
            .Include(b => b.Cargo)
            .Where(b => b.AttackerUserId == userId || b.DefenderUserId == userId)
            .OrderByDescending(b => b.AcceptedAtUtc).ThenByDescending(b => b.Id)
            .Take(take)
            .ToListAsync();

        var names = await NamesAsync(context, battles.SelectMany(b => new[] { b.AttackerUserId, b.DefenderUserId }));
        return battles
            .Select(b => new PvpBattleView(b, names.GetValueOrDefault(b.AttackerUserId, "Unknown"), names.GetValueOrDefault(b.DefenderUserId, "Unknown")))
            .ToList();
    }

    private static Task<Dictionary<string, string>> NamesAsync(ApplicationDbContext context, IEnumerable<string> userIds)
    {
        var ids = userIds.Distinct().ToList();
        return context.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => string.IsNullOrWhiteSpace(u.Nickname) ? u.UserName ?? "Unknown" : u.Nickname);
    }
}
