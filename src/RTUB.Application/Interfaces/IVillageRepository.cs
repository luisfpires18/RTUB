using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for Village entity with eager loading of children.
/// </summary>
public interface IVillageRepository : IRepository<Village>
{
    /// <summary>
    /// Get a village by user ID with all child entities included.
    /// </summary>
    Task<Village?> GetByUserIdAsync(string userId);

    /// <summary>
    /// Get a village by user ID with all child entities included (tracked for updates).
    /// </summary>
    Task<Village?> GetByUserIdTrackedAsync(string userId);
}
