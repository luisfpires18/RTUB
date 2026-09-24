using RTUB.Core.Entities.AfterHours;
using RTUB.Core.Enums.AfterHours;

namespace RTUB.Core.Helpers.AfterHours;

/// <summary>What a crime will cost and pay with a given approach, for a given (reconciled) state.</summary>
public sealed record CrimeOdds(int Chance, long Cash, long Xp, long FailureXp, int Heat);

/// <summary>
/// Crime balance (Game Manual v2). The only place success chance and approach-adjusted rewards are
/// computed; the service executes with it and the UI previews with it.
///
/// Rounding: every multiplier is applied in integer arithmetic and rounded half up
/// (35 × 1.25 = 43.75 → 44; 26 × 1.1 = 28.6 → 29; 26 × 0.25 = 6.5 → 7). Careful heat is half,
/// rounded up (5 → 3). Failure XP is 25% of the approach-adjusted XP.
/// </summary>
public static class CrimeRules
{
    public const int MinChance = 15;
    public const int MaxChance = 95;
    public const int HeatBlockThreshold = 80;

    public static CrimeOdds Odds(CrimeDefinition crime, CrimeApproach approach, PlayerCycleState state)
    {
        var (chanceMod, cashNum, cashDen, xpNum, xpDen) = approach switch
        {
            CrimeApproach.Careful => (8, 4, 5, 1, 1),
            CrimeApproach.Standard => (0, 1, 1, 1, 1),
            CrimeApproach.Bold => (-8, 5, 4, 11, 10),
            _ => throw new ArgumentOutOfRangeException(nameof(approach), approach, "Unknown approach")
        };

        var chance = crime.BaseChance
            + 2 * (SkillRank(state, crime.Skill) - 4)
            - 2 * (state.Heat / 10)
            + chanceMod;

        var heat = approach switch
        {
            CrimeApproach.Careful => (crime.Heat + 1) / 2,
            CrimeApproach.Bold => crime.Heat + 4,
            _ => crime.Heat
        };

        var xp = RoundHalfUp(crime.Xp, xpNum, xpDen);
        return new CrimeOdds(
            Math.Clamp(chance, MinChance, MaxChance),
            RoundHalfUp(crime.Cash, cashNum, cashDen),
            xp,
            RoundHalfUp(xp, 1, 4),
            heat);
    }

    public static int SkillRank(PlayerCycleState state, PlayerSkill skill) => skill switch
    {
        PlayerSkill.Toughness => state.Toughness,
        PlayerSkill.Stealth => state.Stealth,
        PlayerSkill.Smarts => state.Smarts,
        PlayerSkill.Charisma => state.Charisma,
        _ => throw new ArgumentOutOfRangeException(nameof(skill), skill, "Unknown skill")
    };

    /// <summary><c>value × num / den</c> for non-negative values, rounded half up.</summary>
    internal static long RoundHalfUp(long value, long num, long den) => (2 * value * num + den) / (2 * den);
}
