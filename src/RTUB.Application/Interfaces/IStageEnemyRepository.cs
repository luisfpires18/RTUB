using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Interface for Stage Enemy repository
/// Handles data access for stage enemy templates
/// </summary>
public interface IStageEnemyRepository : IRepository<StageEnemy>
{
    /// <summary>
    /// Gets enemies for a specific region
    /// </summary>
    /// <param name="region">The region type</param>
    /// <returns>List of enemies in that region</returns>
    Task<List<StageEnemy>> GetByRegionAsync(RegionType region);

    /// <summary>
    /// Gets enemies by type and region
    /// </summary>
    /// <param name="type">The enemy type</param>
    /// <param name="region">The region type</param>
    /// <returns>List of enemies matching the criteria</returns>
    Task<List<StageEnemy>> GetByTypeAndRegionAsync(EnemyType type, RegionType region);

    /// <summary>
    /// Gets a random enemy for a stage
    /// </summary>
    /// <param name="type">The enemy type needed</param>
    /// <param name="region">The region type</param>
    /// <returns>A random enemy template, or null if none found</returns>
    Task<StageEnemy?> GetRandomEnemyAsync(EnemyType type, RegionType region);
}
