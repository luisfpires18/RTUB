using RTUB.Core.Entities;
using RTUB.Core.Utilities;
using RTUB.Application.DTOs;

namespace RTUB.Application.Services;

/// <summary>
/// Holds all server-side state for an interactive combat session.
/// Lives on the Razor page component (in-memory, per Blazor circuit).
/// If the circuit drops, the battle is lost — same as the existing watchdog behavior.
/// </summary>
public class CombatSession
{
    // ── Identification ──
    public int Seed { get; set; }
    public SeededRandom Rng { get; set; } = null!;
    public string Mode { get; set; } = "stage"; // "arena" | "stage" | "boss"

    // ── Combatant State ──
    public CombatantState Player { get; set; } = null!;
    public List<CombatantState> Enemies { get; set; } = new();
    public int CurrentTargetIndex { get; set; }

    // ── Buff Tracking ──
    public int CigarroShieldRemaining { get; set; }
    public int CanhaoBoostRemaining { get; set; }
    public bool HasShotBuff { get; set; }
    public bool HasPenaltyBuff { get; set; }

    // ── Status Effect Tracking ──
    /// <summary>
    /// Sleep turns remaining per enemy (key = enemy identifier, value = turns left).
    /// While sleeping, enemy speed bar is frozen.
    /// </summary>
    public Dictionary<string, int> EnemySleepTurns { get; set; } = new();

    /// <summary>
    /// Vulnerable stacks per enemy (key = enemy identifier, value = hits remaining).
    /// Next attack deals double damage.
    /// </summary>
    public Dictionary<string, int> EnemyVulnerableStacks { get; set; } = new();

    /// <summary>
    /// Bleed ticks per enemy (key = enemy identifier, value = (ticksRemaining, damagePerTick)).
    /// </summary>
    public Dictionary<string, (int TicksRemaining, int DamagePerTick)> EnemyBleed { get; set; } = new();

    /// <summary>
    /// Slow effect per enemy (key = enemy identifier, value = (hitsRemaining, slowFraction)).
    /// </summary>
    public Dictionary<string, (int HitsRemaining, double SlowFraction)> EnemySlow { get; set; } = new();

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
    public Dictionary<string, (int HitsRemaining, double ReductionFraction)> EnemyPowerReduction { get; set; } = new();

    /// <summary>
    /// Enemy defense break (key = enemy identifier, value = (hitsRemaining, reductionFraction)).
    /// Guitarra power chord reduces enemy defense.
    /// </summary>
    public Dictionary<string, (int HitsRemaining, double ReductionFraction)> EnemyDefenseBreak { get; set; } = new();

    // ── Spell State ──
    /// <summary>
    /// Player's equipped spells for this battle.
    /// </summary>
    public List<SpecialAttack> EquippedSpells { get; set; } = new();

    /// <summary>
    /// Remaining cooldown per spell (key = AttackId, value = remaining seconds).
    /// Starts at 0 (all spells ready at battle start).
    /// </summary>
    public Dictionary<string, double> SpellCooldowns { get; set; } = new();

    /// <summary>
    /// Remaining cooldown per consumable (key = consumable type name e.g. "fino", value = remaining seconds).
    /// Persists across stage transitions.
    /// </summary>
    public Dictionary<string, double> ConsumableCooldowns { get; set; } = new();

    // ── Timing (for server-side validation) ──
    public DateTime BattleStartedAt { get; set; }
    public DateTime LastPlayerActionAt { get; set; }
    public DateTime[] LastEnemyActionAt { get; set; } = Array.Empty<DateTime>();

    // ── Recording ──
    /// <summary>
    /// All events recorded during the battle — saved as ReplayJson at the end.
    /// </summary>
    public List<CombatEvent> RecordedEvents { get; set; } = new();
    public int EventTimestamp { get; set; }
    public bool IsComplete { get; set; }
    public string? Winner { get; set; }
}

/// <summary>
/// State of a single combatant (player or enemy) during interactive combat.
/// </summary>
public class CombatantState
{
    public string Identifier { get; set; } = string.Empty;  // "Player", "Enemy0", "Enemy1", etc.
    public string Name { get; set; } = string.Empty;
    public int CurrentHP { get; set; }
    public int MaxHP { get; set; }
    public int Power { get; set; }
    public int Defense { get; set; }
    public double CriticalChance { get; set; }
    public double ActionTimeSeconds { get; set; } // seconds between auto-attacks
}
