using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository for CharacterStageProgress entity operations
/// </summary>
public interface ICharacterStageProgressRepository : IRepository<CharacterStageProgress>
{
    /// <summary>
    /// Get all progress records for a character, including stage details
    /// </summary>
    /// <param name="characterId">The character ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of character stage progress records ordered by stage number</returns>
    Task<List<CharacterStageProgress>> GetCharacterProgressAsync(int characterId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get progress for a specific character and stage
    /// </summary>
    /// <param name="characterId">The character ID</param>
    /// <param name="stageId">The stage ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The character stage progress if found, otherwise null</returns>
    Task<CharacterStageProgress?> GetProgressAsync(int characterId, int stageId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if a character has completed a stage at least once
    /// </summary>
    /// <param name="characterId">The character ID</param>
    /// <param name="stageId">The stage ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the character has completed the stage at least once, otherwise false</returns>
    Task<bool> HasCompletedStageAsync(int characterId, int stageId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get or create progress record for a character and stage
    /// Creates a new progress record if one doesn't exist, otherwise returns the existing record
    /// </summary>
    /// <param name="characterId">The character ID</param>
    /// <param name="stageId">The stage ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The character stage progress record (existing or newly created)</returns>
    Task<CharacterStageProgress> GetOrCreateProgressAsync(int characterId, int stageId, CancellationToken cancellationToken = default);
}
