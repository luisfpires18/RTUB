using RTUB.Core.Entities.AfterHours;
using RTUB.Core.Enums.AfterHours;

namespace RTUB.Core.Helpers.AfterHours;

/// <summary>A server-owned objective. Stable <see cref="Key"/>; values are copied onto progress rows when used.</summary>
public sealed record ObjectiveDefinition(
    string Key, string Title, string Description, ObjectiveMetric Metric, long Target,
    long XpReward = 0, int Points = 0, ObjectiveCategory? Category = null);

/// <summary>The game week containing an instant: 1-based index and its [start, end) bounds.</summary>
public sealed record GameWeek(int Index, DateTime StartUtc, DateTime EndUtc);

/// <summary>
/// Objectives and championship scoring.
/// Game Manual v2: daily objectives (small rewards, no championship points), weekly individual and family
/// objectives, automatic completion, two championships scored as the sum of the best 12 weekly scores,
/// individual weekly caps 40 solo crime / 20 cargo-contracts / 20 PvP / 20 family participation = 100,
/// same-family PvP excluded, family points stay where they were earned.
/// <b>AH-008 defaults:</b> cycle-relative 7-day weeks, the catalogues and thresholds below, 3-of-5 daily
/// rotation, alternating A/B weekly sets, ⌊10 × loot multiplier⌋ individual PvP points, family cap 100.
/// </summary>
public static class ObjectiveCatalogue
{
    public const int DailyPerDay = 3;
    public const int IndividualWeeklyCap = 100;
    public const int FamilyWeeklyCap = 100;
    public const int BestWeeks = 12;
    public const int PvpPointsPerWin = 10;
    public const string PvpObjectiveKey = "WPVP";
    public static readonly TimeSpan WeekLength = TimeSpan.FromDays(7);

    public static int CategoryCap(ObjectiveCategory category) => category == ObjectiveCategory.SoloCrime ? 40 : 20;

    public static readonly IReadOnlyList<ObjectiveDefinition> Daily =
    [
        new("D01", "Working Night", "Complete 3 successful crimes", ObjectiveMetric.SuccessfulCrimes, 3, XpReward: 15),
        new("D02", "Cool It", "Reduce heat with a cover job", ObjectiveMetric.CoverJobsReducingHeat, 1, XpReward: 10),
        new("D03", "Burn the Clock", "Spend 60 energy", ObjectiveMetric.EnergySpent, 60, XpReward: 15),
        new("D04", "Move the Goods", "Sell or deliver 1 cargo unit", ObjectiveMetric.CargoMoved, 1, XpReward: 10),
        new("D05", "Settle a Score", "Win a PvP battle, attacking or defending", ObjectiveMetric.PvpWins, 1, XpReward: 15),
    ];

    /// <summary>Individual PvP: up to 20 points, ⌊10 × loot multiplier⌋ per distinct target beaten as attacker.</summary>
    public static readonly ObjectiveDefinition WeeklyPvp =
        new(PvpObjectiveKey, "Make Your Name", "Beat distinct outside-family targets as attacker (10 × loot multiplier each)",
            ObjectiveMetric.QualifiedPvpWins, 20, Points: 20, Category: ObjectiveCategory.Pvp);

    private static readonly IReadOnlyList<ObjectiveDefinition> WeeklySetA =
    [
        new("WA1", "Busy Week", "Complete 12 successful crimes", ObjectiveMetric.SuccessfulCrimes, 12, Points: 20, Category: ObjectiveCategory.SoloCrime),
        new("WA2", "Pay Day", "Earn 1,500 cash from successful crimes", ObjectiveMetric.CrimeCash, 1_500, Points: 20, Category: ObjectiveCategory.SoloCrime),
        new("WA3", "Clear the Stock", "Fence 10 cargo units", ObjectiveMetric.FencedUnits, 10, Points: 20, Category: ObjectiveCategory.CargoContracts),
        WeeklyPvp,
        new("WA5", "Pay Your Dues", "Donate 1,000 to your family treasury", ObjectiveMetric.Donated, 1_000, Points: 20, Category: ObjectiveCategory.FamilyParticipation),
    ];

    private static readonly IReadOnlyList<ObjectiveDefinition> WeeklySetB =
    [
        new("WB1", "Crime Wave", "Complete 15 successful crimes", ObjectiveMetric.SuccessfulCrimes, 15, Points: 20, Category: ObjectiveCategory.SoloCrime),
        new("WB2", "All Nighter", "Spend 240 energy on crimes", ObjectiveMetric.CrimeEnergy, 240, Points: 20, Category: ObjectiveCategory.SoloCrime),
        new("WB3", "Trusted Supplier", "Complete 2 buyer contracts", ObjectiveMetric.ContractsDelivered, 2, Points: 20, Category: ObjectiveCategory.CargoContracts),
        WeeklyPvp,
        new("WB5", "Family Business", "Complete 10 successful crimes while in a family", ObjectiveMetric.SuccessfulCrimesAsFamilyMember, 10, Points: 20, Category: ObjectiveCategory.FamilyParticipation),
    ];

    private static readonly IReadOnlyList<ObjectiveDefinition> FamilySetA =
    [
        new("FA1", "Crew Hustle", "Family members complete 30 successful crimes", ObjectiveMetric.SuccessfulCrimes, 30, Points: 25),
        new("FA2", "War Chest", "Donate 3,000 to the treasury", ObjectiveMetric.Donated, 3_000, Points: 25),
        new("FA3", "Supply Line", "Fence 20 cargo units", ObjectiveMetric.FencedUnits, 20, Points: 25),
        new("FA4", "Show of Force", "Win 4 PvP attacks on distinct outside targets", ObjectiveMetric.QualifiedPvpWins, 4, Points: 25),
    ];

    private static readonly IReadOnlyList<ObjectiveDefinition> FamilySetB =
    [
        new("FB1", "Crew Takeover", "Family members complete 40 successful crimes", ObjectiveMetric.SuccessfulCrimes, 40, Points: 25),
        new("FB2", "Keep the Lights On", "Donate 2,000 to the treasury", ObjectiveMetric.Donated, 2_000, Points: 25),
        new("FB3", "Reliable Crew", "Complete 4 buyer contracts", ObjectiveMetric.ContractsDelivered, 4, Points: 25),
        new("FB4", "Show of Force", "Win 4 PvP attacks on distinct outside targets", ObjectiveMetric.QualifiedPvpWins, 4, Points: 25),
    ];

    /// <summary>Today's 3 dailies: consecutive templates starting at DayNumber mod 5, the same for everyone.</summary>
    public static IReadOnlyList<ObjectiveDefinition> DailyFor(DateOnly lisbonDay) =>
        Enumerable.Range(0, DailyPerDay).Select(i => Daily[(lisbonDay.DayNumber + i) % Daily.Count]).ToList();

    /// <summary>Odd weeks use set A, even weeks set B.</summary>
    public static IReadOnlyList<ObjectiveDefinition> WeeklyFor(int week) => week % 2 == 1 ? WeeklySetA : WeeklySetB;

    public static IReadOnlyList<ObjectiveDefinition> FamilyFor(int week) => week % 2 == 1 ? FamilySetA : FamilySetB;

    public static ObjectiveDefinition? Find(string key) =>
        Daily.Concat(WeeklySetA).Concat(WeeklySetB).Concat(FamilySetA).Concat(FamilySetB).FirstOrDefault(d => d.Key == key);

    /// <summary>
    /// AH-008 default: week 1 starts at the cycle start; weeks are consecutive 7-day [start, end)
    /// intervals, the last one cut short by the cycle end.
    /// </summary>
    public static GameWeek WeekOf(GameCycle cycle, DateTime utcNow)
    {
        var index = (int)((utcNow - cycle.StartUtc).Ticks / WeekLength.Ticks) + 1;
        var start = cycle.StartUtc + WeekLength * (index - 1);
        var end = start + WeekLength;
        return new GameWeek(index, start, end < cycle.EndUtc ? end : cycle.EndUtc);
    }

    // ------------------------------------------------------------------ scores

    /// <summary>Individual weekly score from stored awarded points, applying category caps and the 100 cap.</summary>
    public static int IndividualWeeklyScore(IEnumerable<PlayerObjectiveProgress> weeklyRows) =>
        Math.Min(IndividualWeeklyCap, CategoryScores(weeklyRows).Values.Sum());

    public static IReadOnlyDictionary<ObjectiveCategory, int> CategoryScores(IEnumerable<PlayerObjectiveProgress> weeklyRows)
    {
        var rows = weeklyRows.Where(r => r.Period == ObjectivePeriod.Weekly && r.Category is not null).ToList();
        return Enum.GetValues<ObjectiveCategory>().ToDictionary(
            c => c,
            c => Math.Min(CategoryCap(c), rows.Where(r => r.Category == c).Sum(r => r.PointsAwarded)));
    }

    public static int FamilyWeeklyScore(IEnumerable<FamilyObjectiveProgress> rows) =>
        Math.Min(FamilyWeeklyCap, rows.Sum(r => r.PointsAwarded));

    /// <summary>Sum of the best 12 weekly scores (all of them if fewer), and which weeks counted.</summary>
    public static (int Total, IReadOnlyList<int> CountedWeeks) BestWeeksTotal(IReadOnlyDictionary<int, int> scoreByWeek)
    {
        var best = scoreByWeek.Where(w => w.Value > 0)
            .OrderByDescending(w => w.Value).ThenBy(w => w.Key)
            .Take(BestWeeks).ToList();
        return (best.Sum(w => w.Value), best.Select(w => w.Key).OrderBy(w => w).ToList());
    }

    /// <summary>⌊10 × loot multiplier⌋ for one credited win.</summary>
    public static int PvpPoints(decimal lootMultiplier) => (int)decimal.Floor(PvpPointsPerWin * lootMultiplier);
}
