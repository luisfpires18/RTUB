using Microsoft.EntityFrameworkCore;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;


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

    /// <summary>
    /// Initializes a new instance of the ActivityService
    /// </summary>
    /// <param name="activityRepository">Repository for activity operations</param>
    public ActivityService(IActivityRepository activityRepository)
    {
        _activityRepository = activityRepository;
    }

    /// <summary>
    /// Gets an activity by its ID with all transactions included
    /// </summary>
    /// <param name="id">The ID of the activity to retrieve</param>
    /// <returns>The activity if found, null otherwise</returns>
    public async Task<Activity?> GetActivityByIdAsync(int id)
    {
        // Get activity with transactions for computed properties
        return await _activityRepository.GetWithTransactionsAsync(id);
    }

    /// <summary>
    /// Gets all activities with their transactions included
    /// </summary>
    /// <returns>Collection of all activities with transactions</returns>
    public async Task<IEnumerable<Activity>> GetAllActivitiesAsync()
    {
        // Use query to include Transactions for computed properties
        return await _activityRepository.Query()
            .AsNoTracking()
            .Include(a => a.Transactions)
            .ToListAsync();
    }

    /// <summary>
    /// Gets all activities for a specific report, ordered by date descending
    /// </summary>
    /// <param name="reportId">The ID of the report</param>
    /// <returns>Collection of activities for the specified report, ordered by latest date first</returns>
    public async Task<IEnumerable<Activity>> GetActivitiesByReportIdAsync(int reportId)
    {
        // Use query to filter and include Transactions for computed properties
        // Order by latest date (EndDate if available, otherwise StartDate) descending
        return await _activityRepository.Query()
            .AsNoTracking()
            .Include(a => a.Transactions)
            .Where(a => a.ReportId == reportId)
            .OrderByDescending(a => a.EndDate ?? a.StartDate)
            .ToListAsync();
    }

    /// <summary>
    /// Creates a new activity
    /// </summary>
    /// <param name="reportId">The ID of the report this activity belongs to</param>
    /// <param name="name">The name of the activity</param>
    /// <param name="startDate">The start date of the activity</param>
    /// <param name="description">Optional description of the activity</param>
    /// <param name="endDate">Optional end date of the activity</param>
    /// <returns>The created activity</returns>
    public async Task<Activity> CreateActivityAsync(int reportId, string name, DateTime startDate, string? description = null, DateTime? endDate = null)
    {
        var activity = Activity.Create(reportId, name, startDate, description, endDate);
        return await _activityRepository.AddAsync(activity);
    }

    /// <summary>
    /// Updates an existing activity
    /// </summary>
    /// <param name="id">The ID of the activity to update</param>
    /// <param name="name">The new name for the activity</param>
    /// <param name="startDate">The new start date</param>
    /// <param name="description">The new description (can be null)</param>
    /// <param name="endDate">The new end date (can be null)</param>
    /// <exception cref="EntityNotFoundException">Thrown when the activity is not found</exception>
    public async Task UpdateActivityAsync(int id, string name, DateTime startDate, string? description, DateTime? endDate = null)
    {
        var activity = await _activityRepository.GetByIdOrThrowAsync(id);
        var wasLocked = activity.IsLocked; // Preserve lock status
        activity.UpdateDetails(name, startDate, description, endDate);
        activity.IsLocked = wasLocked; // Restore lock status
        await _activityRepository.UpdateAsync(activity);
    }

    /// <summary>
    /// Locks an activity, marking it as done and preventing new transactions
    /// </summary>
    /// <param name="id">The ID of the activity to lock</param>
    /// <exception cref="EntityNotFoundException">Thrown when the activity is not found</exception>
    public async Task LockActivityAsync(int id)
    {
        var activity = await _activityRepository.GetByIdOrThrowAsync(id);
        activity.Lock();
        await _activityRepository.UpdateAsync(activity);
    }

    /// <summary>
    /// Unlocks an activity, allowing new transactions to be added
    /// </summary>
    /// <param name="id">The ID of the activity to unlock</param>
    /// <exception cref="EntityNotFoundException">Thrown when the activity is not found</exception>
    public async Task UnlockActivityAsync(int id)
    {
        var activity = await _activityRepository.GetByIdOrThrowAsync(id);
        activity.Unlock();
        await _activityRepository.UpdateAsync(activity);
    }

    /// <summary>
    /// Deletes an activity and all its transactions
    /// </summary>
    /// <param name="id">The ID of the activity to delete</param>
    /// <exception cref="EntityNotFoundException">Thrown when the activity is not found</exception>
    public async Task DeleteActivityAsync(int id)
    {
        var activity = await _activityRepository.GetWithTransactionsAsync(id);

        if (activity == null)
            throw new EntityNotFoundException(nameof(Activity), id);

        await _activityRepository.DeleteAsync(activity);
    }
}
