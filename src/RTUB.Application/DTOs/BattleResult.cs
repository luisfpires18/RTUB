using RTUB.Core.Enums;

namespace RTUB.Application.DTOs;

/// <summary>
/// Result of a battle including outcome, rewards, and replay data
/// This is returned directly to the client - battles are not persisted to database
/// </summary>
public class BattleResult
{
    /// <summary>
    /// Unique identifier for this battle (for replay/finalization purposes)
    /// </summary>
    public Guid BattleId { get; set; }

    /// <summary>
    /// The attacker's character ID
    /// </summary>
    public int AttackerCharacterId { get; set; }

    /// <summary>
    /// The defender's character ID
    /// </summary>
    public int DefenderCharacterId { get; set; }

    /// <summary>
    /// RNG seed used for deterministic combat
    /// </summary>
    public int Seed { get; set; }

    /// <summary>
    /// Outcome of the battle
    /// </summary>
    public BattleOutcome Outcome { get; set; }

    /// <summary>
    /// XP reward for the attacker
    /// </summary>
    public int AttackerXP { get; set; }

    /// <summary>
    /// Fidelis reward for the attacker
    /// </summary>
    public decimal AttackerFidelis { get; set; }

    /// <summary>
    /// Replay JSON data for battle animation
    /// </summary>
    public string ReplayJson { get; set; } = string.Empty;

    /// <summary>
    /// Attacker's final HP after the battle
    /// </summary>
    public long AttackerFinalHP { get; set; }

    /// <summary>
    /// Whether shot buff was active during this battle
    /// </summary>
    public bool ShotBuffUsed { get; set; }

    /// <summary>
    /// Whether shot buff expired after this battle
    /// </summary>
    public bool ShotBuffExpired { get; set; }

    /// <summary>
    /// Remaining shot buff battles after this battle
    /// </summary>
    public int ShotBuffBattlesRemaining { get; set; }

    /// <summary>
    /// Whether the penalty buff was active during this battle
    /// </summary>
    public bool PenaltyBuffUsed { get; set; }

    /// <summary>
    /// Whether the penalty buff expired after this battle
    /// </summary>
    public bool PenaltyBuffExpired { get; set; }
}
