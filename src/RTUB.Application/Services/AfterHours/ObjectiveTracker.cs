using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Core.Entities.AfterHours;
using RTUB.Core.Enums.AfterHours;
using RTUB.Core.Helpers.AfterHours;

namespace RTUB.Application.Services.AfterHours;

/// <summary>
/// Objective progress for one accepted action. Called by the action service inside the action's own
/// write transaction, after the action is accepted and before its single save: progress, completions,
/// daily XP and weekly points commit (or roll back) with the action. Replays and refusals never reach
/// it, so they never advance anything. Family attribution uses the membership active right now, under
/// the write lock, and is stored on the family's rows; later membership changes do not move it.
/// </summary>
internal static class ObjectiveTracker
{
    public static async Task RecordAsync(ApplicationDbContext context, GameCycle cycle, PlayerCycleState actor, PlayerActionReceipt receipt, DateTime now)
    {
        var metrics = ObjectiveRules.ActorMetrics(receipt);
        var battle = receipt.PvpBattle;
        if (metrics.Count == 0 && battle is null)
            return;

        var day = LisbonCalendar.DateOf(now);
        var week = ObjectiveCatalogue.WeekOf(cycle, now).Index;
        var familyId = await ActiveFamilyAsync(context, actor.UserId);
        if (familyId is not null && metrics.TryGetValue(ObjectiveMetric.SuccessfulCrimes, out var crimes))
            metrics[ObjectiveMetric.SuccessfulCrimesAsFamilyMember] = crimes;

        var familyPvpWin = false;
        if (battle is { AttackerWon: true })
            familyPvpWin = await CreditPvpWinAsync(context, cycle, actor, battle, familyId, week, now);

        await AdvanceDailyAsync(context, cycle, actor, day, metrics, now);
        if (battle is { AttackerWon: false } && FindTracked(context, battle.DefenderStateId) is { } defender)
            await AdvanceDailyAsync(context, cycle, defender, day, new Dictionary<ObjectiveMetric, long> { [ObjectiveMetric.PvpWins] = 1 }, now);

        foreach (var definition in ObjectiveCatalogue.WeeklyFor(week).Where(d => d.Key != ObjectiveCatalogue.PvpObjectiveKey))
        {
            if (!metrics.TryGetValue(definition.Metric, out var amount)) continue;
            var row = await PlayerRowAsync(context, cycle, actor, ObjectivePeriod.Weekly, week, definition);
            if (ObjectiveRules.Advance(row, amount, now)) row.PointsAwarded = definition.Points;
        }

        if (familyId is { } family)
        {
            foreach (var definition in ObjectiveCatalogue.FamilyFor(week))
            {
                var amount = definition.Metric == ObjectiveMetric.QualifiedPvpWins
                    ? (familyPvpWin ? 1 : 0)
                    : metrics.GetValueOrDefault(definition.Metric);
                if (amount <= 0) continue;
                var row = await FamilyRowAsync(context, cycle, family, week, definition);
                if (ObjectiveRules.Advance(row, amount, now)) row.PointsAwarded = definition.Points;
            }
        }
    }

    private static async Task AdvanceDailyAsync(ApplicationDbContext context, GameCycle cycle, PlayerCycleState player, DateOnly day,
        IReadOnlyDictionary<ObjectiveMetric, long> metrics, DateTime now)
    {
        foreach (var definition in ObjectiveCatalogue.DailyFor(day))
        {
            if (!metrics.TryGetValue(definition.Metric, out var amount)) continue;
            var row = await PlayerRowAsync(context, cycle, player, ObjectivePeriod.Daily, day.DayNumber, definition);
            if (!ObjectiveRules.Advance(row, amount, now)) continue;
            row.XpAwarded = definition.XpReward;
            player.AddXp(definition.XpReward);
        }
    }

    /// <summary>
    /// Records a qualifying attacker win (outside the attacker's family at battle time, target not new-player
    /// protected) and awards individual points for the first win over that target this week, capped at 20.
    /// Returns whether it also counts for the attacker's family (first win over that target by the family).
    /// </summary>
    private static async Task<bool> CreditPvpWinAsync(ApplicationDbContext context, GameCycle cycle, PlayerCycleState attacker,
        PvpBattle battle, int? familyId, int week, DateTime now)
    {
        var defender = FindTracked(context, battle.DefenderStateId)
            ?? await context.AfterHoursPlayerCycleStates.AsNoTracking().SingleAsync(s => s.Id == battle.DefenderStateId);
        if (PvpRules.HasNewPlayerProtection(defender, battle.AcceptedAtUtc))
            return false;
        var defenderFamily = await ActiveFamilyAsync(context, defender.UserId);
        if (familyId is not null && familyId == defenderFamily)
            return false; // same family: no points, no family progress

        var individual = !await context.AfterHoursPvpObjectiveCredits.AnyAsync(c =>
            c.GameCycleId == cycle.Id && c.Week == week && c.AttackerStateId == attacker.Id && c.DefenderUserId == defender.UserId && c.CountsForIndividual);
        var forFamily = familyId is not null && !await context.AfterHoursPvpObjectiveCredits.AnyAsync(c =>
            c.GameCycleId == cycle.Id && c.Week == week && c.FamilyId == familyId && c.DefenderUserId == defender.UserId && c.CountsForFamily);

        var points = 0;
        if (individual)
        {
            var row = await PlayerRowAsync(context, cycle, attacker, ObjectivePeriod.Weekly, week, ObjectiveCatalogue.WeeklyPvp);
            points = Math.Min(ObjectiveCatalogue.PvpPoints(battle.LootMultiplier), ObjectiveCatalogue.CategoryCap(ObjectiveCategory.Pvp) - row.PointsAwarded);
            row.PointsAwarded += points;
            row.Progress = row.PointsAwarded;
            if (row.PointsAwarded >= row.Target && row.CompletedAtUtc is null) row.CompletedAtUtc = now;
        }

        context.AfterHoursPvpObjectiveCredits.Add(new PvpObjectiveCredit
        {
            GameCycleId = cycle.Id,
            Week = week,
            PvpBattle = battle,
            AttackerStateId = attacker.Id,
            DefenderUserId = defender.UserId,
            CountsForIndividual = individual,
            IndividualPoints = points,
            FamilyId = familyId,
            CountsForFamily = forFamily
        });
        return forFamily;
    }

    private static PlayerCycleState? FindTracked(ApplicationDbContext context, int stateId) =>
        context.AfterHoursPlayerCycleStates.Local.FirstOrDefault(s => s.Id == stateId);

    private static Task<int?> ActiveFamilyAsync(ApplicationDbContext context, string userId) =>
        context.AfterHoursFamilyMemberships.AsNoTracking()
            .Where(m => m.UserId == userId && m.LeftAtUtc == null)
            .Select(m => (int?)m.FamilyId)
            .SingleOrDefaultAsync();

    private static async Task<PlayerObjectiveProgress> PlayerRowAsync(ApplicationDbContext context, GameCycle cycle, PlayerCycleState player,
        ObjectivePeriod period, int periodKey, ObjectiveDefinition definition)
    {
        var existing = await context.AfterHoursPlayerObjectiveProgress.SingleOrDefaultAsync(r =>
            r.PlayerCycleStateId == player.Id && r.Period == period && r.PeriodKey == periodKey && r.ObjectiveKey == definition.Key);
        if (existing is not null) return existing;

        var row = new PlayerObjectiveProgress
        {
            GameCycleId = cycle.Id,
            PlayerCycleStateId = player.Id,
            Period = period,
            PeriodKey = periodKey,
            ObjectiveKey = definition.Key,
            Category = definition.Category,
            Target = definition.Target
        };
        context.AfterHoursPlayerObjectiveProgress.Add(row);
        return row;
    }

    private static async Task<FamilyObjectiveProgress> FamilyRowAsync(ApplicationDbContext context, GameCycle cycle, int familyId,
        int week, ObjectiveDefinition definition)
    {
        var existing = await context.AfterHoursFamilyObjectiveProgress.SingleOrDefaultAsync(r =>
            r.FamilyId == familyId && r.GameCycleId == cycle.Id && r.Week == week && r.ObjectiveKey == definition.Key);
        if (existing is not null) return existing;

        var row = new FamilyObjectiveProgress
        {
            FamilyId = familyId,
            GameCycleId = cycle.Id,
            Week = week,
            ObjectiveKey = definition.Key,
            Target = definition.Target
        };
        context.AfterHoursFamilyObjectiveProgress.Add(row);
        return row;
    }
}
