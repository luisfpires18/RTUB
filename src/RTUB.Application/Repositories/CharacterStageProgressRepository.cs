using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for CharacterStageProgress entity
/// Provides specialized data access methods for character stage progression tracking
/// </summary>
public class CharacterStageProgressRepository : Repository<CharacterStageProgress>, ICharacterStageProgressRepository
{
    public CharacterStageProgressRepository(ApplicationDbContext context) : base(context)
    {
    }
    
    /// <summary>
    /// Get all progress records for a character, including stage details
    /// </summary>
    /// <param name="characterId">The character ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of character stage progress records ordered by stage number</returns>
    public async Task<List<CharacterStageProgress>> GetCharacterProgressAsync(int characterId, CancellationToken cancellationToken = default)
    {
        return await _context.CharacterStageProgress
            .AsNoTracking()
            .Include(csp => csp.Stage)
            .Where(csp => csp.CharacterId == characterId)
            .OrderBy(csp => csp.Stage.StageNumber)
            .ToListAsync(cancellationToken);
    }
    
    /// <summary>
    /// Get progress for a specific character and stage
    /// Includes stage details in the result
    /// </summary>
    /// <param name="characterId">The character ID</param>
    /// <param name="stageId">The stage ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The character stage progress if found, otherwise null</returns>
    public async Task<CharacterStageProgress?> GetProgressAsync(int characterId, int stageId, CancellationToken cancellationToken = default)
    {
        return await _context.CharacterStageProgress
            .Include(csp => csp.Stage)
            .FirstOrDefaultAsync(csp => csp.CharacterId == characterId && csp.StageId == stageId, cancellationToken);
    }
    
    /// <summary>
    /// Check if a character has completed a stage at least once
    /// Uses efficient Any() query for optimal performance
    /// </summary>
    /// <param name="characterId">The character ID</param>
    /// <param name="stageId">The stage ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the character has completed the stage at least once, otherwise false</returns>
    public async Task<bool> HasCompletedStageAsync(int characterId, int stageId, CancellationToken cancellationToken = default)
    {
        return await _context.CharacterStageProgress
            .AsNoTracking()
            .AnyAsync(csp => csp.CharacterId == characterId && csp.StageId == stageId && csp.CompletionCount > 0, cancellationToken);
    }
    
    /// <summary>
    /// Get or create progress record for a character and stage
    /// Creates a new progress record if one doesn't exist, otherwise returns the existing record
    /// This method ensures that a progress record always exists for tracking
    /// </summary>
    /// <param name="characterId">The character ID</param>
    /// <param name="stageId">The stage ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The character stage progress record (existing or newly created)</returns>
    public async Task<CharacterStageProgress> GetOrCreateProgressAsync(int characterId, int stageId, CancellationToken cancellationToken = default)
    {
        var existing = await GetProgressAsync(characterId, stageId, cancellationToken);
        
        if (existing != null)
        {
            return existing;
        }
        
        // Create new progress record using factory method
        var newProgress = CharacterStageProgress.Create(characterId, stageId);
        await AddAsync(newProgress);
        
        return newProgress;
    }
}
