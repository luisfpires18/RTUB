namespace RTUB.Core.Helpers.AfterHours;

/// <summary>Skill training, Game Manual v2.</summary>
public static class TrainingRules
{
    public const int MaxStoredPoints = 3;
    public const int EnergyCost = 20;

    /// <summary>Wallet cash to raise a skill from <paramref name="currentRank"/>: 120 + 40·(rank − 4).</summary>
    public static long CashCost(int currentRank) => 120 + 40L * (currentRank - 4);

    /// <summary>Highest trainable rank at <paramref name="level"/>: min(12, 4 + ⌊level / 2⌋).</summary>
    public static int SkillCap(int level) => Math.Min(12, 4 + level / 2);
}
