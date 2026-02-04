using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Interface for battle service
/// Handles battle creation, simulation, and reward distribution
/// </summary>
public interface IBattleService
{
    /// <summary>
    /// Creates and executes a battle vs AI opponent
    /// </summary>
    /// <param name="playerCharacterId">The player's character ID</param>
    /// <returns>The created battle record</returns>
    Task<Battle> CreateBattleVsAIAsync(int playerCharacterId);

    /// <summary>
    /// Creates and executes a battle vs a specific opponent character
    /// Rewards are NOT applied until FinalizeAndApplyRewardsAsync is called
    /// </summary>
    /// <param name="playerCharacterId">The player's character ID</param>
    /// <param name="opponentCharacterId">The opponent character ID</param>
    /// <returns>The created battle record</returns>
    Task<Battle> CreateBattleVsOpponentAsync(int playerCharacterId, int opponentCharacterId);

    /// <summary>
    /// Finalizes a battle and applies rewards
    /// Should be called after the battle animation finishes
    /// </summary>
    /// <param name="battleId">The battle ID</param>
    /// <returns>True if rewards were applied, false if already applied or battle not found</returns>
    Task<bool> FinalizeAndApplyRewardsAsync(int battleId);
}
