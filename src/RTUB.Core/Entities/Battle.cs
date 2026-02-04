using System.ComponentModel.DataAnnotations;
using RTUB.Core.Enums;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a battle between a player and an AI opponent
/// All battles are player vs AI (other players' characters used as CPU)
/// </summary>
public class Battle : BaseEntity
{
    [Required]
    public int AttackerCharacterId { get; set; }

    [Required]
    public int DefenderCharacterId { get; set; }

    // Note: All battles are vs AI (other players' characters used as CPU opponents)
    // Defender is always an AI opponent (another player's character, but treated as CPU)

    [Required]
    public int Seed { get; set; }  // RNG seed for determinism

    [Required]
    public BattleOutcome Outcome { get; set; }  // AttackerWon, DefenderWon, Draw

    // Rewards (stored as deltas)
    // Note: Defender rewards are always 0 (AI opponents don't receive rewards)
    public int AttackerXP { get; set; } = 0;
    public int DefenderXP { get; set; } = 0;  // Always 0 for AI
    public decimal AttackerFidelis { get; set; } = 0m;
    public decimal DefenderFidelis { get; set; } = 0m;  // Always 0 for AI

    // Replay data (JSON)
    public string ReplayJson { get; set; } = string.Empty;

    /// <summary>
    /// Whether rewards have been applied for this battle
    /// Rewards are only applied after the battle animation finishes
    /// </summary>
    public bool RewardsApplied { get; set; } = false;

    /// <summary>
    /// Attacker's final HP after battle (pending application)
    /// </summary>
    public int? AttackerFinalHP { get; set; }

    /// <summary>
    /// Whether shot buff was used in this battle (for decrementing counter)
    /// </summary>
    public bool ShotBuffUsed { get; set; } = false;

    /// <summary>
    /// Whether shot buff expired after this battle (for HP scaling)
    /// </summary>
    public bool ShotBuffExpired { get; set; } = false;

    // Navigation
    public virtual Character Attacker { get; set; } = null!;
    public virtual Character Defender { get; set; } = null!;

    // Private constructor for EF Core
    private Battle() { }

    /// <summary>
    /// Factory method to create a new battle
    /// </summary>
    public static Battle Create(int attackerCharacterId, int defenderCharacterId, int seed, BattleOutcome outcome)
    {
        if (attackerCharacterId <= 0)
            throw new ArgumentException("Attacker character ID must be greater than 0", nameof(attackerCharacterId));
        if (defenderCharacterId <= 0)
            throw new ArgumentException("Defender character ID must be greater than 0", nameof(defenderCharacterId));
        if (attackerCharacterId == defenderCharacterId)
            throw new ArgumentException("Attacker and defender cannot be the same character", nameof(defenderCharacterId));

        return new Battle
        {
            AttackerCharacterId = attackerCharacterId,
            DefenderCharacterId = defenderCharacterId,
            Seed = seed,
            Outcome = outcome,
            AttackerXP = 0,
            DefenderXP = 0,
            AttackerFidelis = 0m,
            DefenderFidelis = 0m,
            ReplayJson = string.Empty
        };
    }

    /// <summary>
    /// Sets the rewards for the battle
    /// Note: Defender rewards are always 0 (AI opponents don't receive rewards)
    /// </summary>
    public void SetRewards(int attackerXP, decimal attackerFidelis)
    {
        if (attackerXP < 0)
            throw new ArgumentException("Attacker XP cannot be negative", nameof(attackerXP));
        if (attackerFidelis < 0)
            throw new ArgumentException("Attacker Fidelis cannot be negative", nameof(attackerFidelis));

        AttackerXP = attackerXP;
        AttackerFidelis = attackerFidelis;
        // Defender rewards always remain 0
        DefenderXP = 0;
        DefenderFidelis = 0m;
    }

    /// <summary>
    /// Sets the replay JSON data
    /// </summary>
    public void SetReplay(string replayJson)
    {
        if (string.IsNullOrWhiteSpace(replayJson))
            throw new ArgumentException("Replay JSON cannot be null or empty", nameof(replayJson));

        ReplayJson = replayJson;
    }

    /// <summary>
    /// Marks the rewards as applied
    /// </summary>
    public void MarkRewardsApplied()
    {
        RewardsApplied = true;
    }
}
