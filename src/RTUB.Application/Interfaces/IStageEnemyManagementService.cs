using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for Stage Enemy management (owner CRUD operations)
/// </summary>
public interface IStageEnemyManagementService
{
    /// <summary>
    /// Gets all stage enemies
    /// </summary>
    Task<IEnumerable<StageEnemy>> GetAllAsync();

    /// <summary>
    /// Gets a stage enemy by ID
    /// </summary>
    Task<StageEnemy?> GetByIdAsync(int id);

    /// <summary>
    /// Gets stage enemies filtered by region
    /// </summary>
    Task<IEnumerable<StageEnemy>> GetByRegionAsync(RegionType region);

    /// <summary>
    /// Gets stage enemies filtered by enemy type
    /// </summary>
    Task<IEnumerable<StageEnemy>> GetByTypeAsync(EnemyType type);

    /// <summary>
    /// Creates a new stage enemy
    /// </summary>
    Task<StageEnemy> CreateAsync(
        string name,
        EnemyType type,
        RegionType region,
        int baseHP,
        int basePower,
        int baseSpeed,
        int baseDefense,
        double baseCriticalChance,
        decimal baseFidelisDrop,
        double finoDropChance,
        double shotDropChance,
        string? spritePath,
        int? bossStageNumber,
        PlacementType placement);

    /// <summary>
    /// Updates an existing stage enemy
    /// </summary>
    Task UpdateAsync(
        int id,
        string name,
        EnemyType type,
        RegionType region,
        int baseHP,
        int basePower,
        int baseSpeed,
        int baseDefense,
        double baseCriticalChance,
        decimal baseFidelisDrop,
        double finoDropChance,
        double shotDropChance,
        string? spritePath,
        int? bossStageNumber,
        PlacementType placement);

    /// <summary>
    /// Deletes a stage enemy by ID
    /// </summary>
    Task DeleteAsync(int id);
}
