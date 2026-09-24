namespace RTUB.Core.Helpers.AfterHours;

/// <summary>
/// The After Hours XP curve: cumulative XP needed to reach level L is
/// <c>12·(L−1)³ + 88·(L−1)</c>, capped at level <see cref="MaxLevel"/> for year one.
/// The single place levels are derived; nothing else computes them.
/// </summary>
public static class AfterHoursLevels
{
    public const int MaxLevel = 20;

    /// <summary>Total XP needed to reach <paramref name="level"/> (level 1 = 0).</summary>
    public static long XpForLevel(int level)
    {
        if (level < 1 || level > MaxLevel)
            throw new ArgumentOutOfRangeException(nameof(level), level, $"Level must be 1..{MaxLevel}");

        long n = level - 1;
        return 12 * n * n * n + 88 * n;
    }

    /// <summary>Highest level whose threshold <paramref name="xp"/> has reached, never above <see cref="MaxLevel"/>.</summary>
    public static int LevelForXp(long xp)
    {
        var level = 1;
        while (level < MaxLevel && xp >= XpForLevel(level + 1))
            level++;
        return level;
    }
}
