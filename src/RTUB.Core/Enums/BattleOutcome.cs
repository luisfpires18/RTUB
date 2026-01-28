namespace RTUB.Core.Enums;

/// <summary>
/// Represents the outcome of a battle
/// </summary>
public enum BattleOutcome
{
    /// <summary>
    /// The attacker (player) won the battle
    /// </summary>
    AttackerWon = 0,

    /// <summary>
    /// The defender (AI opponent) won the battle
    /// </summary>
    DefenderWon = 1,

    /// <summary>
    /// The battle ended in a draw
    /// </summary>
    Draw = 2
}
