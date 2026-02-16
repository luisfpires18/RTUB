using RTUB.Core.Entities;
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

    /// <summary>RNG seed for deterministic replay.</summary>
    public int Seed { get; set; }

    /// <summary>Seeded RNG instance for this battle.</summary>
    public SeededRandom Rng { get; set; } = null!;

    /// <summary>Game mode: "arena", "stage", or "boss".</summary>
    public string Mode { get; set; } = "stage";

    // ── Combatant State ──

    /// <summary>Player combatant state.</summary>
    public CombatantState Player { get; set; } = null!;

    /// <summary>Enemy combatant states.</summary>
    public List<CombatantState> Enemies { get; set; } = [];

    /// <summary>Index of the enemy the player is currently targeting.</summary>
    public int CurrentTargetIndex { get; set; }

    // ── Buff Tracking ──

    /// <summary>Remaining Cigarro shield hit absorptions.</summary>
    public int CigarroShieldRemaining { get; set; }

    /// <summary>Remaining Canhão damage boost hits.</summary>
    public int CanhaoBoostRemaining { get; set; }

    /// <summary>Whether the player has an active shot buff.</summary>
    public bool HasShotBuff { get; set; }

    /// <summary>Whether the player has an active penalty buff.</summary>
    public bool HasPenaltyBuff { get; set; }

    /// <summary>
    /// Damage bonus multiplier for heavy attacks from Powers upgrades (e.g., 0.10 = +10%).
    /// </summary>
    public double HeavyAttackDamageBonus { get; set; }

    /// <summary>
    /// Damage bonus multiplier for special attacks (non-heavy) from Powers upgrades (e.g., 0.10 = +10%).
    /// </summary>
    public double SpecialAttackDamageBonus { get; set; }

    // ── Status Effect Tracking ──

    /// <summary>
    /// Sleep turns remaining per enemy (key = enemy identifier, value = turns left).
    /// While sleeping, enemy speed bar is frozen.
    /// </summary>
    public Dictionary<string, int> EnemySleepTurns { get; set; } = [];

    /// <summary>
    /// Vulnerable stacks per enemy (key = enemy identifier, value = hits remaining).
    /// Next attack deals double damage.
    /// </summary>
    public Dictionary<string, int> EnemyVulnerableStacks { get; set; } = [];

    /// <summary>
    /// Bleed ticks per enemy (key = enemy identifier, value = (ticksRemaining, damagePerTick)).
    /// </summary>
    public Dictionary<string, (int TicksRemaining, int DamagePerTick)> EnemyBleed { get; set; } = [];

    /// <summary>
    /// Slow effect per enemy (key = enemy identifier, value = (hitsRemaining, slowFraction)).
    /// </summary>
    public Dictionary<string, (int HitsRemaining, double SlowFraction)> EnemySlow { get; set; } = [];

    /// <summary>
    /// Player power boost stacks (hits remaining, boost fraction).
    /// </summary>
    public (int HitsRemaining, double BoostFraction) PlayerPowerBoost { get; set; }

    /// <summary>
    /// Player haste stacks (hits remaining, speed factor).
    /// </summary>
    public (int HitsRemaining, double SpeedFraction) PlayerHaste { get; set; }

    /// <summary>
    /// Player defense boost stacks (hits remaining, boost fraction).
    /// </summary>
    public (int HitsRemaining, double BoostFraction) PlayerDefenseBoost { get; set; }

    /// <summary>
    /// Player regen effect (ticks remaining, heal per tick as fraction of MaxHP).
    /// </summary>
    public (int TicksRemaining, double HealFraction) PlayerRegen { get; set; }

    /// <summary>
    /// Player instrument shield (hits remaining that are fully absorbed).
    /// </summary>
    public int InstrumentShieldHits { get; set; }

    /// <summary>
    /// Enemy power reduction (key = enemy identifier, value = (hitsRemaining, reductionFraction)).
    /// Saxofone jazz solo reduces enemy power.
    /// </summary>
    public Dictionary<string, (int HitsRemaining, double ReductionFraction)> EnemyPowerReduction { get; set; } = [];

    /// <summary>
    /// Enemy defense break (key = enemy identifier, value = (hitsRemaining, reductionFraction)).
    /// Guitarra power chord reduces enemy defense.
    /// </summary>
    public Dictionary<string, (int HitsRemaining, double ReductionFraction)> EnemyDefenseBreak { get; set; } = [];

    // ── Spell State ──

    /// <summary>
    /// Player's equipped spells for this battle.
    /// </summary>
    public List<SpecialAttack> EquippedSpells { get; set; } = [];

    /// <summary>
    /// Remaining cooldown per spell (key = AttackId, value = remaining seconds).
    /// Starts at 0 (all spells ready at battle start).
    /// </summary>
    public Dictionary<string, double> SpellCooldowns { get; set; } = [];

    /// <summary>
    /// Remaining cooldown per consumable (key = consumable type name e.g. "fino", value = remaining seconds).
    /// Persists across stage transitions.
    /// </summary>
    public Dictionary<string, double> ConsumableCooldowns { get; set; } = [];

    // ── Timing (for server-side validation) ──

    /// <summary>When the battle started (UTC).</summary>
    public DateTime BattleStartedAt { get; set; }

    /// <summary>When the player last attacked (UTC).</summary>
    public DateTime LastPlayerActionAt { get; set; }

    /// <summary>When each enemy last attacked (UTC), indexed by enemy order.</summary>
    public DateTime[] LastEnemyActionAt { get; set; } = [];

    // ── Recording ──

    /// <summary>
    /// All events recorded during the battle — saved as ReplayJson at the end.
    /// </summary>
    public List<CombatEvent> RecordedEvents { get; set; } = [];

    /// <summary>Monotonically increasing event counter for ordering.</summary>
    public int EventTimestamp { get; set; }

    /// <summary>Whether the battle has finished.</summary>
    public bool IsComplete { get; set; }

    /// <summary>Identifier of the winning side, or <c>null</c> if still in progress.</summary>
    public string? Winner { get; set; }
}

/// <summary>
/// State of a single combatant (player or enemy) during interactive combat.
/// </summary>
public class CombatantState
{
    /// <summary>Role identifier: "Player", "Enemy0", "Enemy1", "Attacker", "Defender", etc.</summary>
    public string Identifier { get; set; } = string.Empty;

    /// <summary>Display name (username or enemy label).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Current hit points.</summary>
    public int CurrentHP { get; set; }

    /// <summary>Maximum hit points.</summary>
    public int MaxHP { get; set; }

    /// <summary>Effective power stat.</summary>
    public int Power { get; set; }

    /// <summary>Effective defense stat.</summary>
    public int Defense { get; set; }

    /// <summary>Critical hit probability (0.0–1.0).</summary>
    public double CriticalChance { get; set; }

    /// <summary>Seconds between auto-attacks (lower = faster).</summary>
    public double ActionTimeSeconds { get; set; }
}
