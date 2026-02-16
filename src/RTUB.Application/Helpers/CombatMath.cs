using RTUB.Core.Configuration;
using RTUB.Core.Entities;
using RTUB.Core.Utilities;

namespace RTUB.Application.Helpers;

/// <summary>
/// Shared combat math formulas used by both <see cref="Services.DeterministicCombatEngine"/>
/// and <see cref="Services.CombatActionService"/>. Centralised here to eliminate duplication
/// and guarantee identical calculations across deterministic and interactive combat.
/// </summary>
/// <remarks>
/// Damage pipeline: Power → Variance(0.8–1.2) → Critical(2×) → Defense mitigation(K/(K+def)) → MinDamage floor.
/// </remarks>
public static class CombatMath
{
    /// <summary>Minimum damage variance multiplier (inclusive).</summary>
    public const double DamageVarianceMin = 0.8;

    /// <summary>Maximum damage variance multiplier (inclusive).</summary>
    public const double DamageVarianceMax = 1.2;

    /// <summary>Canhão consumable damage multiplier (+30%).</summary>
    public const double CanhaoDamageMultiplier = 1.30;

    /// <summary>
    /// Calculates raw damage with variance and critical hit, before defense mitigation.
    /// Order of operations: Base Power → Variance → Critical.
    /// </summary>
    /// <param name="power">Attacker's effective power stat.</param>
    /// <param name="criticalChance">Probability of critical hit (0.0–1.0).</param>
    /// <param name="rng">Seeded RNG for deterministic results.</param>
    /// <returns>Raw damage (before defense) and whether the hit was critical.</returns>
    public static (int RawDamage, bool IsCritical) CalculateRawDamage(int power, double criticalChance, SeededRandom rng)
    {
        var variance = rng.Next(DamageVarianceMin, DamageVarianceMax);
        var damage = power * variance;
        var isCritical = rng.NextDouble() < criticalChance;
        if (isCritical)
        {
            damage *= 2;
        }

        return ((int)Math.Round(damage, MidpointRounding.AwayFromZero), isCritical);
    }

    /// <summary>
    /// Applies defense mitigation to raw damage using diminishing returns formula.
    /// Formula: <c>multiplier = K / (K + defense)</c>. Never reaches zero — every point of defense
    /// always helps, but with diminishing returns (no hard cap needed).
    /// </summary>
    /// <param name="rawDamage">Damage after power/crit calculations, before mitigation.</param>
    /// <param name="defense">Target's total defense stat.</param>
    /// <returns>Final damage after defense mitigation (minimum <see cref="MyTunoScaling.MinDamage"/>).</returns>
    public static int ApplyDefenseMitigation(int rawDamage, int defense)
    {
        var k = MyTunoScaling.DefenseK;
        var minDamage = MyTunoScaling.MinDamage;

        var multiplier = k / (k + defense);
        var mitigatedDamage = (int)Math.Floor(rawDamage * multiplier);

        return Math.Max(minDamage, mitigatedDamage);
    }

    /// <summary>
    /// Calculates final damage including defense mitigation.
    /// Order of operations: Power → Variance → Critical → Defense.
    /// </summary>
    /// <param name="power">Attacker's effective power stat.</param>
    /// <param name="criticalChance">Probability of critical hit (0.0–1.0).</param>
    /// <param name="targetDefense">Target's total defense stat.</param>
    /// <param name="rng">Seeded RNG for deterministic results.</param>
    /// <returns>Final damage and whether the hit was critical.</returns>
    public static (int Damage, bool IsCritical) CalculateDamage(int power, double criticalChance, int targetDefense, SeededRandom rng)
    {
        var (rawDamage, isCritical) = CalculateRawDamage(power, criticalChance, rng);
        var finalDamage = ApplyDefenseMitigation(rawDamage, targetDefense);
        return (finalDamage, isCritical);
    }

    /// <summary>
    /// Calculates spell damage (raw, before defense mitigation).
    /// Spells use <see cref="SpecialAttack.DamageMultiplier"/> and optionally always-crit.
    /// </summary>
    /// <param name="power">Caster's effective power stat.</param>
    /// <param name="spell">The spell being cast.</param>
    /// <param name="rng">Seeded RNG for deterministic results.</param>
    /// <returns>Raw spell damage before defense mitigation.</returns>
    public static int CalculateSpellDamage(int power, SpecialAttack spell, SeededRandom rng)
    {
        var variance = rng.Next(DamageVarianceMin, DamageVarianceMax);
        var damage = power * variance * spell.DamageMultiplier;

        if (spell.CanCrit)
        {
            // Spells that can crit always crit (premium feel)
            damage *= 2;
        }

        return (int)Math.Round(damage, MidpointRounding.AwayFromZero);
    }
}
