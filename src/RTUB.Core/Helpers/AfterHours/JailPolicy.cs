namespace RTUB.Core.Helpers.AfterHours;

/// <summary>
/// Jail after a failed crime. <b>AH-003 implementation choice, not Game Manual v2</b>: the manual
/// says only that some failures jail the player for a few minutes, scaling with crime level and
/// heat. Tune here; nothing else encodes these numbers.
/// Both formulas use the heat the crime was attempted at (reconciled, before the crime's own heat).
/// </summary>
public static class JailPolicy
{
    /// <summary><c>10 + RequiredLevel + ⌊heat / 5⌋</c> percent, clamped to 10..60.</summary>
    public static int Chance(int requiredLevel, int heat) =>
        Math.Clamp(10 + requiredLevel + heat / 5, 10, 60);

    /// <summary><c>2 + ⌊RequiredLevel / 5⌋ + ⌊heat / 20⌋</c> minutes, at most 8.</summary>
    public static TimeSpan Duration(int requiredLevel, int heat) =>
        TimeSpan.FromMinutes(Math.Min(8, 2 + requiredLevel / 5 + heat / 20));
}
