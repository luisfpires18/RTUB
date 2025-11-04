using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for Activity operations
/// Abstracts business logic from presentation layer
/// </summary>
public interface IActivityService
{
    Task<Activity?> GetActivityByIdAsync(int id);
    Task<IEnumerable<Activity>> GetAllActivitiesAsync();
    Task<IEnumerable<Activity>> GetActivitiesByReportIdAsync(int reportId);
    Task<Activity> CreateActivityAsync(int reportId, string name, string? description = null);
    Task UpdateActivityAsync(int id, string name, string? description);
    Task RecalculateActivityFinancialsAsync(int id);
    Task DeleteActivityAsync(int id);
}
