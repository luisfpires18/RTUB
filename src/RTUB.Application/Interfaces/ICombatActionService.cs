using RTUB.Application.DTOs;
using RTUB.Application.Services;
using RTUB.Core.Entities;

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
    /// Advances spell cooldowns by the given elapsed time (called by JS after each tick).
    /// </summary>
    Dictionary<string, double> TickCooldowns(CombatSession session, double elapsedSeconds);
}
