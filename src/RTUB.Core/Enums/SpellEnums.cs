namespace RTUB.Core.Enums;

/// <summary>
/// Visual effect type for special attacks / spells.
/// Determines which PixiJS animation plays.
/// </summary>
public enum SpellVfxType
{
    /// <summary>
    /// Colored circle / sprite launched from caster to target (e.g. fireball, ice shard)
    /// </summary>
    Projectile = 0,

    /// <summary>
    /// Glowing line from caster to target with flash (e.g. lightning, laser)
    /// </summary>
    Beam = 1,

    /// <summary>
    /// Expanding ring hitting all enemies (e.g. earthquake, shockwave)
    /// </summary>
    AreaOfEffect = 2,

    /// <summary>
    /// Aura pulse on self — heal, shield, power-up
    /// </summary>
    Buff = 3,

    /// <summary>
    /// Enhanced melee strike with extra VFX (e.g. heavy slash, power punch)
    /// </summary>
    MeleeStrike = 4,

    /// <summary>
    /// Musical wave / sound-based VFX (for instrument specials)
    /// </summary>
    SoundWave = 5,

    /// <summary>
    /// Swirling notes / musical notation particles
    /// </summary>
    MusicNotes = 6
}

/// <summary>
/// Who the spell targets.
/// </summary>
public enum SpellTargetType
{
    /// <summary>
    /// Hits the current target enemy
    /// </summary>
    SingleEnemy = 0,

    /// <summary>
    /// Hits all alive enemies (AoE)
    /// </summary>
    AllEnemies = 1,

    /// <summary>
    /// Targets self (heal, buff, shield)
    /// </summary>
    Self = 2
}

/// <summary>
/// Special status effects that instrument abilities can apply.
/// </summary>
public enum SpellEffectType
{
    /// <summary>
    /// No special effect — just damage/heal
    /// </summary>
    None = 0,

    /// <summary>
    /// Target cannot attack for N turns (speed bar frozen)
    /// </summary>
    Sleep = 1,

    /// <summary>
    /// Target takes extra damage on next hit
    /// </summary>
    Vulnerable = 2,

    /// <summary>
    /// Caster gets a damage boost for N attacks
    /// </summary>
    PowerBoost = 3,

    /// <summary>
    /// Caster gains a temporary shield absorbing N hits
    /// </summary>
    Shield = 4,

    /// <summary>
    /// Target loses HP over time (damage per tick)
    /// </summary>
    Bleed = 5,

    /// <summary>
    /// Target's attack speed is slowed
    /// </summary>
    Slow = 6,

    /// <summary>
    /// Caster attacks faster for a duration
    /// </summary>
    Haste = 7,

    /// <summary>
    /// Target's defense is reduced
    /// </summary>
    DefenseBreak = 8,

    /// <summary>
    /// Caster's defense is increased
    /// </summary>
    DefenseBoost = 9,

    /// <summary>
    /// Heal over time
    /// </summary>
    Regen = 10
}
