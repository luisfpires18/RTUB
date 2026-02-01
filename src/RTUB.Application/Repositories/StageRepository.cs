using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for Stage entity
/// Provides specialized data access methods for stage operations
/// </summary>
public class StageRepository : Repository<Stage>, IStageRepository
{
    public StageRepository(ApplicationDbContext context) : base(context)
    {
    }
    
    /// <summary>
    /// Get all active stages ordered by stage number
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of active stages ordered by stage number</returns>
    public async Task<List<Stage>> GetAllActiveStagesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Stages
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.StageNumber)
            .ToListAsync(cancellationToken);
    }
    
    /// <summary>
    /// Get a stage by its stage number
    /// </summary>
    /// <param name="stageNumber">The stage number (1-12)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The stage if found, otherwise null</returns>
    public async Task<Stage?> GetByStageNumberAsync(int stageNumber, CancellationToken cancellationToken = default)
    {
        return await _context.Stages
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.StageNumber == stageNumber && s.IsActive, cancellationToken);
    }
    
    /// <summary>
    /// Get stages available for a character's level
    /// Returns stages where RequiredLevel is less than or equal to characterLevel
    /// </summary>
    /// <param name="characterLevel">The character's current level</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of stages available for the character's level ordered by stage number</returns>
    public async Task<List<Stage>> GetStagesForLevelAsync(int characterLevel, CancellationToken cancellationToken = default)
    {
        return await _context.Stages
            .AsNoTracking()
            .Where(s => s.IsActive && s.RequiredLevel <= characterLevel)
            .OrderBy(s => s.StageNumber)
            .ToListAsync(cancellationToken);
    }
}
