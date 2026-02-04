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
    /// Gets the number of enemies remaining in the current stage
    /// Used for multi-enemy stages where player fights enemies sequentially
    /// </summary>
    /// <param name="userId">The user's ID</param>
    /// <returns>Number of enemies remaining to fight in current stage</returns>
    Task<int> GetRemainingEnemiesInStageAsync(string userId);

    /// <summary>
    /// Checks if the current stage is complete (all enemies defeated)
    /// </summary>
    /// <param name="userId">The user's ID</param>
    /// <returns>True if stage is complete, false if more enemies remain</returns>
    Task<bool> IsStageCompleteAsync(string userId);

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

    /// <summary>
    /// Cancels a stage run in progress.
    /// Restores the character's HP to the specified value and resets stage progress.
    /// Used when user exits mid-run without completing it.
    /// </summary>
    /// <param name="characterId">The character's ID</param>
    /// <param name="restoreHp">The HP value to restore</param>
    /// <param name="restoreStage">The stage number to restore to</param>
    /// <returns>True if cancelled successfully</returns>
    Task<bool> CancelRunAsync(int characterId, int restoreHp, int restoreStage);

    /// <summary>
    /// Gets the biome name for a given stage number
    /// </summary>
    /// <param name="stageNumber">The stage number</param>
    /// <returns>Biome name (e.g., "Forest", "Desert")</returns>
    string GetBiomeNameForStage(int stageNumber);

    /// <summary>
    /// Gets the number of enemies for a given stage
    /// </summary>
    /// <param name="stageNumber">The stage number</param>
    /// <returns>Number of enemies</returns>
    int GetEnemyCountForStage(int stageNumber);

    /// <summary>
    /// Checks if a stage is a boss stage
    /// </summary>
    /// <param name="stageNumber">The stage number</param>
    /// <returns>True if boss stage, false otherwise</returns>
    bool IsBossStage(int stageNumber);
}
