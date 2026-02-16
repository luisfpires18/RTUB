using RTUB.Core.Configuration;
using RTUB.Core.Entities;
using RTUB.Core.Utilities;

namespace RTUB.Application.Helpers;

/// <summary>
/// Shared combat math formulas used by both <see cref="Services.DeterministicCombatEngine"/>
/// (pre-computed battles) and <see cref="Services.CombatActionService"/> (interactive battles).
/// Centralises damage calculations so the two engines can never drift out of sync.
/// </summary>
public static class CombatMath
{
    /// <summary>Minimum variance multiplier applied to base power.</summary>
    public const double DamageVarianceMin = 0.8;

    /// <summary>Maximum variance multiplier applied to base power.</summary>
    public const double DamageVarianceMax = 1.2;

    /// <summary>Canhão consumable damage bonus (+30%).</summary>
    public const double CanhaoDamageMultiplier = 1.30;

    /// <summary>
    /// Calculates raw damage with variance and critical hit.
    /// Order of operations: Base Power → Variance → Critical (×2).
    /// </summary>
    /// <param name="power">Attacker's total power stat.</param>
    /// <param name="criticalChance">Probability of a critical hit (0–1).</param>
    /// <param name="rng">Seeded RNG for deterministic results.</param>
    /// <returns>Raw damage (before defense) and whether a crit occurred.</returns>
    public static (int rawDamage, bool isCritical) CalculateRawDamage(
        int power, double criticalChance, SeededRandom rng)
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
    /// <c>mult = K / (K + defense)</c>.
    /// Never reaches zero — every point of defense always helps, but with diminishing returns.
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
    /// <param name="power">Attacker's total power stat.</param>
    /// <param name="criticalChance">Probability of a critical hit (0–1).</param>
    /// <param name="targetDefense">Target's total defense stat.</param>
    /// <param name="rng">Seeded RNG for deterministic results.</param>
    /// <returns>Final damage and whether a crit occurred.</returns>
    public static (int damage, bool isCritical) CalculateDamage(
        int power, double criticalChance, int targetDefense, SeededRandom rng)
    {
        var (rawDamage, isCritical) = CalculateRawDamage(power, criticalChance, rng);
        var finalDamage = ApplyDefenseMitigation(rawDamage, targetDefense);
        return (finalDamage, isCritical);
    }

    /// <summary>
    /// Calculates spell damage using the spell's <see cref="SpecialAttack.DamageMultiplier"/>.
    /// Spells that have <see cref="SpecialAttack.CanCrit"/> set always crit (premium feel).
    /// Defence is <strong>not</strong> applied — spell damage bypasses armor.
    /// </summary>
    /// <param name="power">Caster's total power stat.</param>
    /// <param name="spell">The spell definition.</param>
    /// <param name="rng">Seeded RNG for deterministic results.</param>
    /// <returns>Final spell damage.</returns>
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
