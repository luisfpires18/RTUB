using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository for Stage entity operations
/// </summary>
public interface IStageRepository : IRepository<Stage>
{
    /// <summary>
    /// Get all active stages ordered by stage number
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of active stages ordered by stage number</returns>
    Task<List<Stage>> GetAllActiveStagesAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get a stage by its stage number
    /// </summary>
    /// <param name="stageNumber">The stage number (1-12)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The stage if found, otherwise null</returns>
    Task<Stage?> GetByStageNumberAsync(int stageNumber, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get stages available for a character's level
    /// Returns stages where RequiredLevel is less than or equal to characterLevel
    /// </summary>
    /// <param name="characterLevel">The character's current level</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of stages available for the character's level</returns>
    Task<List<Stage>> GetStagesForLevelAsync(int characterLevel, CancellationToken cancellationToken = default);
}
