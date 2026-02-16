namespace RTUB.Core.Entities;

/// <summary>
/// Defines a special attack / spell that a character can use during interactive combat.
/// Like Blade Crafter spells — clickable abilities with cooldowns and VFX.
/// </summary>
public class SpecialAttack
{
    public int Id { get; set; }

    /// <summary>
    /// Unique key used in code and JSON (e.g. "fireball", "thunder_strike", "heal")
    /// </summary>
    public string AttackId { get; set; } = string.Empty;

    /// <summary>
    /// Display name shown on the spell button (e.g. "Bola de Fogo")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Short description for tooltip / UI (e.g. "1.5x dano, projétil de fogo")
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Visual effect type for PixiJS rendering
    /// </summary>
    public SpellVfxType VfxType { get; set; } = SpellVfxType.Projectile;

    /// <summary>
    /// Who the spell targets
    /// </summary>
    public SpellTargetType Target { get; set; } = SpellTargetType.SingleEnemy;

    /// <summary>
    /// Damage multiplier applied to base power (e.g. 1.5 = 150% damage).
    /// For heals, this is the heal multiplier.
    /// </summary>
    public double DamageMultiplier { get; set; } = 1.5;

    /// <summary>
    /// Cooldown in seconds — how long before the spell can be used again.
    /// JS tracks this client-side; server validates on each cast.
    /// </summary>
    public double CooldownSeconds { get; set; } = 10.0;

    /// <summary>
    /// Emoji or short string shown on the spell button (e.g. "🔥")
    /// </summary>
    public string Icon { get; set; } = "⚔️";

    /// <summary>
    /// Optional sprite key for projectile / beam animation asset.
    /// Null means use default colored circle.
    /// </summary>
    public string? SpriteKey { get; set; }

    /// <summary>
    /// Hex color used for the VFX (e.g. "ff4400" for fire orange)
    /// </summary>
    public string VfxColor { get; set; } = "ff4400";

    /// <summary>
    /// Whether this spell causes a screen shake on impact
    /// </summary>
    public bool ScreenShake { get; set; }

    /// <summary>
    /// Whether critical hits apply to this spell (false = never crits, uses flat multiplier)
    /// </summary>
    public bool CanCrit { get; set; }

    /// <summary>
    /// Sort order for display in the spell bar
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Status effect applied by this spell (e.g. Sleep, Vulnerable, PowerBoost)
    /// </summary>
    public SpellEffectType EffectType { get; set; } = SpellEffectType.None;

    /// <summary>
    /// Duration/stacks for the effect (e.g. 2 = sleep 2 turns, or 3 = 3 bleed ticks).
    /// Interpretation depends on EffectType.
    /// </summary>
    public int EffectDuration { get; set; }

    /// <summary>
    /// Magnitude of the effect (e.g. 0.3 = 30% speed slow, 0.5 = 50% defense break).
    /// Interpretation depends on EffectType.
    /// </summary>
    public double EffectMagnitude { get; set; }
}
