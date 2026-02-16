using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for Stage Enemy management (owner CRUD operations).
/// </summary>
public interface IStageEnemyManagementService
{
    /// <summary>
    /// Gets all stage enemies.
    /// </summary>
    Task<IEnumerable<StageEnemy>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a stage enemy by ID.
    /// </summary>
    Task<StageEnemy?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets stage enemies filtered by region.
    /// </summary>
    Task<IEnumerable<StageEnemy>> GetByRegionAsync(RegionType region, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets stage enemies filtered by enemy type.
    /// </summary>
    Task<IEnumerable<StageEnemy>> GetByTypeAsync(EnemyType type, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new stage enemy.
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
        PlacementType placement,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing stage enemy.
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
        PlacementType placement,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a stage enemy by ID.
    /// </summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
