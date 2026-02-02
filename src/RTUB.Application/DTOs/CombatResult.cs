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
    public int AttackerFinalHP { get; set; }

    /// <summary>
    /// Final HP of the defender after the battle
    /// </summary>
    public int DefenderFinalHP { get; set; }
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
    public int? Damage { get; set; }

    /// <summary>
    /// Whether the attack was a critical hit (for Attack events)
    /// </summary>
    public bool? IsCritical { get; set; }

    /// <summary>
    /// Character identifier (for HPUpdate, KO events)
    /// </summary>
    public string? Character { get; set; }

    /// <summary>
    /// Current HP value (for HPUpdate events)
    /// </summary>
    public int? HP { get; set; }

    /// <summary>
    /// Maximum HP value (for initial HPUpdate events)
    /// </summary>
    public int? MaxHP { get; set; }

    /// <summary>
    /// Winner character identifier (for Victory events)
    /// </summary>
    public string? Winner { get; set; }

    /// <summary>
    /// Timestamp of the event (sequential order)
    /// </summary>
    public int Timestamp { get; set; }
}
