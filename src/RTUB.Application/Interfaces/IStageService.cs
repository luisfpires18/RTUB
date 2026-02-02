using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Interface for Stage Mode service
/// Handles stage progression, battles, and rewards
/// </summary>
public interface IStageService
{
    /// <summary>
    /// Gets or creates stage progress for a user
    /// </summary>
    /// <param name="userId">The user's ID</param>
    /// <returns>The user's stage progress</returns>
    Task<StageProgress> GetOrCreateStageProgressAsync(string userId);

    /// <summary>
    /// Gets stage progress for a user
    /// </summary>
    /// <param name="userId">The user's ID</param>
    /// <returns>The user's stage progress, or null if not found</returns>
    Task<StageProgress?> GetStageProgressAsync(string userId);

    /// <summary>
    /// Gets the enemy for the current stage
    /// </summary>
    /// <param name="stageProgress">The player's stage progress</param>
    /// <returns>The enemy for the current stage</returns>
    Task<StageEnemy?> GetCurrentStageEnemyAsync(StageProgress stageProgress);

    /// <summary>
    /// Executes a battle on the current stage
    /// </summary>
    /// <param name="characterId">The player's character ID</param>
    /// <returns>The stage battle result</returns>
    Task<StageBattle> ExecuteStageBattleAsync(int characterId);

    /// <summary>
    /// Gets recent stage battle history
    /// </summary>
    /// <param name="characterId">The player's character ID</param>
    /// <param name="count">Number of recent battles to retrieve</param>
    /// <returns>List of recent stage battles</returns>
    Task<List<StageBattle>> GetRecentBattlesAsync(int characterId, int count = 10);

    /// <summary>
    /// Returns the player to their last checkpoint after defeat
    /// </summary>
    /// <param name="userId">The user's ID</param>
    /// <returns>Updated stage progress</returns>
    Task<StageProgress> ReturnToCheckpointAsync(string userId);
}
