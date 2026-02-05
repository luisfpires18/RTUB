using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Interface for battle service
/// Handles battle creation, simulation, and reward distribution
/// Battles are not persisted - only the results (win/loss stats, rewards) are stored on Character
/// </summary>
public interface IBattleService
{
    /// <summary>
    /// Creates and executes a battle vs a specific opponent character
    /// Rewards are NOT applied until FinalizeAndApplyRewardsAsync is called
    /// </summary>
    /// <param name="playerCharacterId">The player's character ID</param>
    /// <param name="opponentCharacterId">The opponent character ID</param>
    /// <returns>The battle result with replay data</returns>
    Task<BattleResult> CreateBattleVsOpponentAsync(int playerCharacterId, int opponentCharacterId);

    /// <summary>
    /// Finalizes a battle and applies rewards
    /// Should be called after the battle animation finishes
    /// </summary>
    /// <param name="result">The battle result to finalize</param>
    /// <returns>True if rewards were applied successfully</returns>
    Task<bool> FinalizeAndApplyRewardsAsync(BattleResult result);
}
