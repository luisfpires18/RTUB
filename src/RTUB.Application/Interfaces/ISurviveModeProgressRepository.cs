using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for Survive Mode progress data access.
/// </summary>
public interface ISurviveModeProgressRepository : IRepository<SurviveModeProgress>
{
    /// <summary>
    /// Gets the survive mode progress for a user by their user ID.
    /// </summary>
    Task<SurviveModeProgress?> GetByUserIdAsync(string userId);
}
