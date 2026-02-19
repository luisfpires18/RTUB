using System.Text.Json.Serialization;
using RTUB.Core.Enums;

namespace RTUB.Application.DTOs;

/// <summary>
/// Result of a combat simulation
/// Contains the outcome, events, and final state
/// </summary>
public class CombatResult
{
    /// <summary>
    /// The outcome of the battle
    /// </summary>
    public BattleOutcome Outcome { get; set; }

    /// <summary>
    /// List of combat events that occurred during the battle
    /// </summary>
    public List<CombatEvent> Events { get; set; } = new();

    /// <summary>
    /// Final HP of the attacker after the battle
    /// </summary>
    public long AttackerFinalHP { get; set; }

    /// <summary>
    /// Final HP of the defender after the battle
    /// </summary>
    public long DefenderFinalHP { get; set; }

}

/// <summary>
/// Represents a single event that occurred during combat
/// Used to generate replay JSON
/// </summary>
public class CombatEvent
{
    /// <summary>
    /// Type of event (RoundStart, Attack, HPUpdate, KO, Victory, RoundEnd)
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Round number (for RoundStart, RoundEnd events)
    /// </summary>
    public int? Round { get; set; }

    /// <summary>
    /// Attacker character identifier (for Attack events)
    /// </summary>
    public string? Attacker { get; set; }

    /// <summary>
    /// Defender character identifier (for Attack events)
    /// </summary>
    public string? Defender { get; set; }

    /// <summary>
    /// Damage dealt (for Attack events)
    /// </summary>
    public long? Damage { get; set; }

    /// <summary>
    /// Whether the attack was a critical hit (for Attack events)
    /// </summary>
    public bool? IsCritical { get; set; }

    /// <summary>
    /// Whether this attack was blocked by a Cigarro shield (damage absorbed)
    /// </summary>
    public bool? IsBlocked { get; set; }

    /// <summary>
    /// Whether this attack was boosted by a Canhão damage buff (+30%)
    /// </summary>
    public bool? IsBoosted { get; set; }

    /// <summary>
    /// Whether this attack was an AOE hit (Canhão buff)
    /// </summary>
    public bool? IsAoe { get; set; }

    /// <summary>
    /// Character identifier (for HPUpdate, KO events)
    /// </summary>
    public string? Character { get; set; }

    /// <summary>
    /// Current HP value (for HPUpdate events)
    /// </summary>
    public long? HP { get; set; }

    /// <summary>
    /// Maximum HP value (for initial HPUpdate events)
    /// </summary>
    public long? MaxHP { get; set; }

    /// <summary>
    /// Winner character identifier (for Victory events)
    /// </summary>
    public string? Winner { get; set; }

    /// <summary>
    /// Timestamp of the event (sequential order)
    /// </summary>
    public int Timestamp { get; set; }

    /// <summary>
    /// Action time in seconds for this character (for initial ActionTime events)
    /// Determines how long until the character can attack
    /// </summary>
    public double? ActionTime { get; set; }

    /// <summary>
    /// Simulation time in milliseconds when this event occurred (for real-time combat)
    /// </summary>
    public double? SimTime { get; set; }

    // ── Spell / Special Attack fields (null-suppressed in JSON for normal attacks) ──

    /// <summary>
    /// Unique key of the special attack used (e.g. "fireball", "heal").
    /// Null for normal auto-attacks.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AttackId { get; set; }

    /// <summary>
    /// Display name of the ability (e.g. "Bola de Fogo").
    /// Null for normal auto-attacks.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AbilityName { get; set; }

    /// <summary>
    /// Visual effect type key for JS VFX dispatch (e.g. "projectile", "beam", "aoe", "buff").
    /// Null for normal auto-attacks.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? VfxType { get; set; }

    /// <summary>
    /// Hex color for the VFX (e.g. "ff4400"). Null uses default.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? VfxColor { get; set; }

    /// <summary>
    /// Visual hint for extra effects (e.g. "screenShake"). Null for none.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? VisualHint { get; set; }

    /// <summary>
    /// For AoE spells — list of defender identifiers hit.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Targets { get; set; }

    /// <summary>
    /// For AoE spells — per-target damage values (same order as Targets).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<long>? TargetDamages { get; set; }

    /// <summary>
    /// Status effect applied by this attack (e.g. "sleep", "bleed", "vulnerable").
    /// Null for attacks with no status effect.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EffectName { get; set; }

    /// <summary>
    /// Duration/stacks of the status effect.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? EffectDuration { get; set; }
}
