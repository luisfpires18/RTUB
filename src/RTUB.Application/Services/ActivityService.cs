using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;
using Microsoft.EntityFrameworkCore;


namespace RTUB.Application.Services;

/// <summary>
/// Activity service implementation using Repository pattern
/// Contains business logic for activity operations
/// Follows Single Responsibility and Dependency Inversion principles
/// Now depends on IActivityRepository abstraction instead of concrete DbContext
/// </summary>
public class ActivityService : IActivityService
{
    private readonly IActivityRepository _activityRepository;

    public ActivityService(IActivityRepository activityRepository)
    {
        _activityRepository = activityRepository;
    }

    public async Task<Activity?> GetActivityByIdAsync(int id)
    {
        // Get activity with transactions for computed properties
        return await _activityRepository.GetWithTransactionsAsync(id);
    }

    public async Task<IEnumerable<Activity>> GetAllActivitiesAsync()
    {
        // Use query to include Transactions for computed properties
        return await _activityRepository.Query()
            .AsNoTracking()
            .Include(a => a.Transactions)
            .ToListAsync();
    }

    public async Task<IEnumerable<Activity>> GetActivitiesByReportIdAsync(int reportId)
    {
        // Use query to filter and include Transactions for computed properties
        return await _activityRepository.Query()
            .AsNoTracking()
            .Include(a => a.Transactions)
            .Where(a => a.ReportId == reportId)
            .OrderBy(a => a.Name)
            .ToListAsync();
    }

    public async Task<Activity> CreateActivityAsync(int reportId, string name, string? description = null)
    {
        var activity = Activity.Create(reportId, name, description);
        return await _activityRepository.AddAsync(activity);
    }

    public async Task UpdateActivityAsync(int id, string name, string? description)
    {
        var activity = await _activityRepository.GetByIdAsync(id);
        if (activity == null)
            throw new EntityNotFoundException(nameof(Activity), id);

        activity.UpdateDetails(name, description);
        await _activityRepository.UpdateAsync(activity);
    }

    public async Task DeleteActivityAsync(int id)
    {
        var activity = await _activityRepository.GetWithTransactionsAsync(id);
            
        if (activity == null)
            throw new EntityNotFoundException(nameof(Activity), id);

        await _activityRepository.DeleteAsync(activity);
    }
}
