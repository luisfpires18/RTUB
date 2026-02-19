using RTUB.Application.DTOs;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for processing individual combat actions during interactive (real-time) battle.
/// Unlike ICombatEngine which pre-computes all events, this processes one action at a time
/// and maintains state in a CombatSession that lives on the Razor page.
///
/// Blade Crafter model: auto-attacks fire automatically, spells are manually triggered.
/// </summary>
public interface ICombatActionService
{
    /// <summary>
    /// Creates a new interactive combat session from Character entities.
    /// </summary>
    CombatSession CreateSession(
        Character player,
        List<Character> enemies,
        int seed,
        List<SpecialAttack>? equippedSpells = null,
        string mode = "stage");

    /// <summary>
    /// Creates a new interactive combat session from pre-built CombatantState objects.
    /// Use this when you don't have full Character entities (e.g., stage enemies from StageBattleEnemyStat).
    /// </summary>
    CombatSession CreateSession(
        Character player,
        List<CombatantState> enemyStates,
        int seed,
        List<SpecialAttack>? equippedSpells = null,
        string mode = "stage");

    /// <summary>
    /// Processes a player auto-attack (speed bar filled, fires automatically).
    /// </summary>
    CombatActionResult ProcessPlayerAutoAttack(CombatSession session);

    /// <summary>
    /// Processes a player spell cast (user clicked a spell button).
    /// </summary>
    CombatActionResult ProcessPlayerSpell(CombatSession session, string attackId);

    /// <summary>
    /// Processes an enemy auto-attack (enemy speed bar filled).
    /// </summary>
    CombatActionResult ProcessEnemyAttack(CombatSession session, int enemyIndex);

    /// <summary>
    /// Advances spell cooldowns by the given elapsed sim-time (called by JS after each tick).
    /// </summary>
    Dictionary<string, double> TickCooldowns(CombatSession session, double elapsedSeconds);

    /// <summary>
    /// Advances consumable cooldowns by real elapsed time (independent of battle speed).
    /// </summary>
    Dictionary<string, double> TickConsumableCooldowns(CombatSession session, double realElapsedSeconds);

    /// <summary>
    /// Applies a consumable item to the current combat session.
    /// Handles inventory consumption, cooldown checks, and effect application.
    /// </summary>
    /// <param name="session">The current combat session (may be null if no active combat).</param>
    /// <param name="userId">The user ID for inventory consumption.</param>
    /// <param name="type">Consumable type key (e.g. "fino", "caneca").</param>
    /// <param name="character">The character entity (for persistent buff tracking).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result describing the outcome, ready to serialize to JSON for JS.</returns>
    Task<ConsumableResult> ApplyConsumableAsync(
        CombatSession? session,
        string userId,
        string type,
        Character character,
        CancellationToken cancellationToken = default);
}
