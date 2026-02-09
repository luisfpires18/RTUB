using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Interface for Boss Mode Progress repository.
/// Handles data access for boss mode progress records.
/// </summary>
public interface IBossModeProgressRepository : IRepository<BossModeProgress>
{
    /// <summary>
    /// Gets boss mode progress for a user.
    /// </summary>
    Task<BossModeProgress?> GetByUserIdAsync(string userId);
}
