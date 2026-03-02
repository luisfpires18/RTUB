using RTUB.Core.Utilities;

namespace RTUB.Application.DTOs;

/// <summary>
/// Holds all server-side state for an interactive combat session.
/// Lives on the Razor page component (in-memory, per Blazor circuit).
/// If the circuit drops, the battle is lost — same as the existing watchdog behavior.
/// </summary>
public class CombatSession
{
    // ── Identification ──

    /// <summary>RNG seed used for deterministic replay.</summary>
    public int Seed { get; set; }

    /// <summary>Seeded random number generator for this session.</summary>
    public SeededRandom Rng { get; set; } = null!;

    /// <summary>Combat mode: "arena", "stage", or "boss".</summary>
    public string Mode { get; set; } = "stage";

    // ── Combatant State ──

    /// <summary>Player's combat state.</summary>
    public CombatantState Player { get; set; } = null!;

    /// <summary>All enemy combat states.</summary>
    public List<CombatantState> Enemies { get; set; } = [];

    /// <summary>Index of the enemy the player is currently targeting.</summary>
    public int CurrentTargetIndex { get; set; }

    // ── Buff Tracking ──

    /// <summary>Whether the Shot consumable buff is active.</summary>
    public bool HasShotBuff { get; set; }

    /// <summary>Whether the Cigarro consumable buff is active.</summary>
    public bool HasCigarroBuff { get; set; }

    /// <summary>Whether the Canhão consumable buff is active.</summary>
    public bool HasCanhaoBuff { get; set; }

    /// <summary>Whether the Penalty consumable buff is active.</summary>
    public bool HasPenaltyBuff { get; set; }

    /// <summary>Effective Cigarro dodge chance (scaled by consumable upgrades).</summary>
    public double EffectiveCigarroDodge { get; set; }

    /// <summary>Effective Penalty lifesteal percent (scaled by consumable upgrades).</summary>
    public double EffectivePenaltyLifesteal { get; set; }

    /// <summary>
    /// Remaining cooldown per consumable (key = consumable type name e.g. "fino", value = remaining seconds).
    /// Persists across stage transitions.
    /// </summary>
    public Dictionary<string, double> ConsumableCooldowns { get; set; } = [];

    // ── Timing (for server-side validation) ──

    /// <summary>When the battle started (UTC).</summary>
    public DateTime BattleStartedAt { get; set; }

    /// <summary>When the player last performed an action (UTC).</summary>
    public DateTime LastPlayerActionAt { get; set; }

    /// <summary>When each enemy last performed an action (UTC), indexed by enemy position.</summary>
    public DateTime[] LastEnemyActionAt { get; set; } = [];

    // ── Recording ──

    /// <summary>
    /// All events recorded during the battle — saved as ReplayJson at the end.
    /// </summary>
    public List<CombatEvent> RecordedEvents { get; set; } = [];

    /// <summary>Monotonically increasing timestamp counter for event ordering.</summary>
    public int EventTimestamp { get; set; }

    /// <summary>Whether the battle has finished.</summary>
    public bool IsComplete { get; set; }

    /// <summary>Identifier of the winner, if the battle is complete.</summary>
    public string? Winner { get; set; }
}

/// <summary>
/// State of a single combatant (player or enemy) during interactive combat.
/// </summary>
public class CombatantState
{
    /// <summary>Unique identifier: "Player", "Attacker", "Defender", "Enemy0", "Enemy1", etc.</summary>
    public string Identifier { get; set; } = string.Empty;

    /// <summary>Display name of the combatant.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Current hit points.</summary>
    public long CurrentHP { get; set; }

    /// <summary>Maximum hit points.</summary>
    public long MaxHP { get; set; }

    /// <summary>Attack power stat.</summary>
    public long Power { get; set; }

    /// <summary>Defense stat.</summary>
    public long Defense { get; set; }

    /// <summary>Probability of a critical hit (0–1).</summary>
    public double CriticalChance { get; set; }

    /// <summary>Seconds between auto-attacks.</summary>
    public double ActionTimeSeconds { get; set; }
}
