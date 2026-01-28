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
    /// </summary>
    /// <param name="playerCharacterId">The player's character ID</param>
    /// <param name="opponentCharacterId">The opponent character ID</param>
    /// <returns>The created battle record</returns>
    Task<Battle> CreateBattleVsOpponentAsync(int playerCharacterId, int opponentCharacterId);
}
