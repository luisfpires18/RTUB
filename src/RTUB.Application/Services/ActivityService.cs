using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;


namespace RTUB.Application.Services;

/// <summary>
/// Activity service implementation
/// Contains business logic for activity operations
/// Follows Single Responsibility and Dependency Inversion principles
/// </summary>
public class ActivityService : IActivityService
{
    private readonly ApplicationDbContext _context;

    public ActivityService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Activity?> GetActivityByIdAsync(int id)
    {
        return await _context.Activities.FindAsync(id);
    }

    public async Task<IEnumerable<Activity>> GetAllActivitiesAsync()
    {
        return await _context.Activities.ToListAsync();
    }

    public async Task<IEnumerable<Activity>> GetActivitiesByReportIdAsync(int reportId)
    {
        return await _context.Activities
            .Where(a => a.ReportId == reportId)
            .OrderBy(a => a.Name)
            .ToListAsync();
    }

    public async Task<Activity> CreateActivityAsync(int reportId, string name, string? description = null)
    {
        var activity = Activity.Create(reportId, name, description);
        _context.Activities.Add(activity);
        await _context.SaveChangesAsync();
        return activity;
    }

    public async Task UpdateActivityAsync(int id, string name, string? description)
    {
        var activity = await _context.Activities.FindAsync(id);
        if (activity == null)
            throw new InvalidOperationException($"Activity with ID {id} not found");

        activity.UpdateDetails(name, description);
        _context.Activities.Update(activity);
        await _context.SaveChangesAsync();
    }

    public async Task RecalculateActivityFinancialsAsync(int id)
    {
        var activity = await _context.Activities.FindAsync(id);
        if (activity == null)
            throw new InvalidOperationException($"Activity with ID {id} not found");

        activity.RecalculateFinancials();
        _context.Activities.Update(activity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteActivityAsync(int id)
    {
        var activity = await _context.Activities.FindAsync(id);
        if (activity == null)
            throw new InvalidOperationException($"Activity with ID {id} not found");

        _context.Activities.Remove(activity);
        await _context.SaveChangesAsync();
    }
}
