using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces.AfterHours;
using RTUB.Core.Entities.AfterHours;
using RTUB.Core.Enums.AfterHours;
using RTUB.Core.Helpers.AfterHours;

namespace RTUB.Application.Services.AfterHours;

public class ObjectiveService(IDbContextFactory<ApplicationDbContext> contextFactory, TimeProvider clock) : IObjectiveService
{
    public async Task<ObjectivesOverview?> GetOverviewAsync(string userId)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        await using var context = await contextFactory.CreateDbContextAsync();
        var cycle = await ActiveCycleAsync(context, now);
        if (cycle is null) return null;

        var day = LisbonCalendar.DateOf(now);
        var week = ObjectiveCatalogue.WeekOf(cycle, now);
        var stateId = await context.AfterHoursPlayerCycleStates.AsNoTracking()
            .Where(s => s.GameCycleId == cycle.Id && s.UserId == userId).Select(s => (int?)s.Id).SingleOrDefaultAsync();

        var rows = stateId is null ? [] : await context.AfterHoursPlayerObjectiveProgress.AsNoTracking()
            .Where(r => r.PlayerCycleStateId == stateId
                && ((r.Period == ObjectivePeriod.Daily && r.PeriodKey == day.DayNumber) || (r.Period == ObjectivePeriod.Weekly && r.PeriodKey == week.Index)))
            .ToListAsync();

        var daily = ObjectiveCatalogue.DailyFor(day)
            .Select(d => View(d, rows.SingleOrDefault(r => r.Period == ObjectivePeriod.Daily && r.ObjectiveKey == d.Key))).ToList();
        var weeklyRows = rows.Where(r => r.Period == ObjectivePeriod.Weekly).ToList();
        var weekly = ObjectiveCatalogue.WeeklyFor(week.Index)
            .Select(d => View(d, weeklyRows.SingleOrDefault(r => r.ObjectiveKey == d.Key))).ToList();

        FamilyObjectivesView? family = null;
        var membership = await context.AfterHoursFamilyMemberships.AsNoTracking().Include(m => m.Family)
            .SingleOrDefaultAsync(m => m.UserId == userId && m.LeftAtUtc == null);
        if (membership is not null)
        {
            var familyRows = await context.AfterHoursFamilyObjectiveProgress.AsNoTracking()
                .Where(r => r.FamilyId == membership.FamilyId && r.GameCycleId == cycle.Id && r.Week == week.Index).ToListAsync();
            family = new FamilyObjectivesView(
                membership.FamilyId,
                membership.Family!.Name,
                ObjectiveCatalogue.FamilyFor(week.Index).Select(d =>
                {
                    var row = familyRows.SingleOrDefault(r => r.ObjectiveKey == d.Key);
                    return new ObjectiveView(d, row?.Progress ?? 0, row?.Target ?? d.Target, row?.CompletedAtUtc is not null, 0, row?.PointsAwarded ?? 0);
                }).ToList(),
                ObjectiveCatalogue.FamilyWeeklyScore(familyRows));
        }

        return new ObjectivesOverview(day, LisbonCalendar.NextMidnightUtc(day), week, daily, weekly,
            ObjectiveCatalogue.CategoryScores(weeklyRows), ObjectiveCatalogue.IndividualWeeklyScore(weeklyRows), family);
    }

    public async Task<Leaderboards?> GetLeaderboardsAsync(string userId)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        await using var context = await contextFactory.CreateDbContextAsync();
        var cycle = await ActiveCycleAsync(context, now);
        if (cycle is null) return null;
        var week = ObjectiveCatalogue.WeekOf(cycle, now).Index;
        var (individual, families) = await StandingsAsync(context, cycle.Id, week, userId);
        return new Leaderboards(week, individual.Select(p => p.Row).ToList(), families);
    }

    /// <summary>
    /// Both championships of a cycle from stored awarded points: every player state of the cycle (with its
    /// user id), and every family with a treasury row or family objective progress in it. Shared by the
    /// live leaderboards and the yearbook archive, so both rank exactly the same way.
    /// </summary>
    internal static async Task<(IReadOnlyList<(StandingRow Row, string UserId)> Individual, IReadOnlyList<StandingRow> Families)> StandingsAsync(
        ApplicationDbContext context, int cycleId, int currentWeek, string? userId)
    {
        // Individual: every player of the cycle, from stored weekly awarded points.
        var players = await context.AfterHoursPlayerCycleStates.AsNoTracking()
            .Where(s => s.GameCycleId == cycleId).Select(s => new { s.Id, s.UserId }).ToListAsync();
        var weeklyRows = await context.AfterHoursPlayerObjectiveProgress.AsNoTracking()
            .Where(r => r.GameCycleId == cycleId && r.Period == ObjectivePeriod.Weekly && r.PointsAwarded > 0).ToListAsync();
        var names = await context.Users.AsNoTracking()
            .Where(u => players.Select(p => p.UserId).Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => string.IsNullOrWhiteSpace(u.Nickname) ? u.UserName ?? "Unknown" : u.Nickname);
        var userIds = players.ToDictionary(p => p.Id, p => p.UserId);
        var individual = Rank(players.Select(p =>
        {
            var byWeek = weeklyRows.Where(r => r.PlayerCycleStateId == p.Id).GroupBy(r => r.PeriodKey)
                .ToDictionary(g => g.Key, g => ObjectiveCatalogue.IndividualWeeklyScore(g));
            return Row(p.Id, names.GetValueOrDefault(p.UserId, "Unknown"), byWeek, currentWeek, p.UserId == userId);
        })).Select(r => (r, userIds[r.Id])).ToList();

        // Families: points stay with the family that earned them, whatever the members do later.
        var familyRows = await context.AfterHoursFamilyObjectiveProgress.AsNoTracking()
            .Where(r => r.GameCycleId == cycleId).ToListAsync();
        var familyIds = familyRows.Select(r => r.FamilyId)
            .Concat(await context.AfterHoursFamilyCycleStates.AsNoTracking().Where(s => s.GameCycleId == cycleId).Select(s => s.FamilyId).ToListAsync())
            .Distinct().ToList();
        var families = await context.AfterHoursFamilies.AsNoTracking().Where(f => familyIds.Contains(f.Id)).ToListAsync();
        var myFamily = userId is null ? null : await context.AfterHoursFamilyMemberships.AsNoTracking()
            .Where(m => m.UserId == userId && m.LeftAtUtc == null).Select(m => (int?)m.FamilyId).SingleOrDefaultAsync();
        var familyStandings = Rank(families.Select(f =>
        {
            var byWeek = familyRows.Where(r => r.FamilyId == f.Id).GroupBy(r => r.Week)
                .ToDictionary(g => g.Key, g => ObjectiveCatalogue.FamilyWeeklyScore(g));
            return Row(f.Id, f.Name, byWeek, currentWeek, f.Id == myFamily);
        }));

        return (individual, familyStandings);
    }

    private static StandingRow Row(int id, string name, IReadOnlyDictionary<int, int> byWeek, int currentWeek, bool mine)
    {
        var (total, counted) = ObjectiveCatalogue.BestWeeksTotal(byWeek);
        return new StandingRow(0, id, name, total, byWeek.GetValueOrDefault(currentWeek), byWeek.Count(w => w.Value > 0), counted, mine);
    }

    /// <summary>Highest first; equal scores share a rank (1, 1, 3). Name order is only for a stable display.</summary>
    private static IReadOnlyList<StandingRow> Rank(IEnumerable<StandingRow> rows)
    {
        var ordered = rows.OrderByDescending(r => r.AnnualScore).ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.Id).ToList();
        return ordered.Select((r, i) => r with { Rank = ordered.FindIndex(o => o.AnnualScore == r.AnnualScore) + 1 }).ToList();
    }

    private static ObjectiveView View(ObjectiveDefinition definition, PlayerObjectiveProgress? row) =>
        new(definition, row?.Progress ?? 0, row?.Target ?? definition.Target, row?.CompletedAtUtc is not null, row?.XpAwarded ?? 0, row?.PointsAwarded ?? 0);

    private static async Task<GameCycle?> ActiveCycleAsync(ApplicationDbContext context, DateTime now)
    {
        var cycle = await context.AfterHoursGameCycles.AsNoTracking().SingleOrDefaultAsync(c => c.Status == GameCycleStatus.Active);
        return cycle is not null && cycle.IsPlayableAt(now) ? cycle : null;
    }
}
