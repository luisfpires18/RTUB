using RTUB.Core.Enums;

namespace RTUB.Application.DTOs;

/// <summary>
/// Result of a single combat action (auto-attack) during interactive combat.
/// Returned from server to JS after each OnPlayerAttack / OnEnemyAttack call.
/// </summary>
public class CombatActionResult
{
    /// <summary>
    /// Events produced by this action (Attack + HPUpdate + maybe KO/Victory).
    /// JS plays these in sequence with animations.
    /// </summary>
    public List<CombatEvent> Events { get; set; } = new();

    /// <summary>
    /// Whether the battle is over after this action.
    /// </summary>
    public bool BattleOver { get; set; }

    /// <summary>
    /// Battle outcome (only set when BattleOver is true).
    /// </summary>
    public BattleOutcome? Outcome { get; set; }
}
