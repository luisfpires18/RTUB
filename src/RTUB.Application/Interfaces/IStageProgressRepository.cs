using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Interface for Stage Progress repository
/// Handles data access for stage progress records
/// </summary>
public interface IStageProgressRepository : IRepository<StageProgress>
{
    /// <summary>
    /// Gets stage progress for a user
    /// </summary>
    /// <param name="userId">The user's ID</param>
    /// <returns>The user's stage progress, or null if not found</returns>
    Task<StageProgress?> GetByUserIdAsync(string userId);
}
