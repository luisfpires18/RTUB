using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Services;

/// <summary>
/// Rehearsal service implementation using Repository pattern
/// Contains business logic for rehearsal operations
/// Now depends on IRehearsalRepository abstraction instead of concrete DbContext
/// </summary>
public class RehearsalService : IRehearsalService
{
    private readonly IRehearsalRepository _rehearsalRepository;
    private readonly IRehearsalAttendanceRepository _attendanceRepository;

    /// <summary>
    /// Initializes a new instance of the RehearsalService
    /// </summary>
    /// <param name="rehearsalRepository">Repository for rehearsal operations</param>
    /// <param name="attendanceRepository">Repository for rehearsal attendance operations</param>
    public RehearsalService(IRehearsalRepository rehearsalRepository, IRehearsalAttendanceRepository attendanceRepository)
    {
        _rehearsalRepository = rehearsalRepository;
        _attendanceRepository = attendanceRepository;
    }

    /// <summary>
    /// Gets a rehearsal by its ID with attendance information
    /// </summary>
    /// <param name="id">The ID of the rehearsal to retrieve</param>
    /// <returns>The rehearsal if found, null otherwise</returns>
    public async Task<Rehearsal?> GetRehearsalByIdAsync(int id)
    {
        return await _rehearsalRepository.GetByIdAsync(id);
    }

    /// <summary>
    /// Gets a rehearsal by its date
    /// </summary>
    /// <param name="date">The date of the rehearsal</param>
    /// <returns>The rehearsal if found, null otherwise</returns>
    public async Task<Rehearsal?> GetRehearsalByDateAsync(DateTime date)
    {
        return await _rehearsalRepository.GetRehearsalByDateAsync(date);
    }

    /// <summary>
    /// Gets all rehearsals within a date range
    /// </summary>
    /// <param name="startDate">The start date of the range</param>
    /// <param name="endDate">The end date of the range</param>
    /// <returns>Collection of rehearsals within the date range, ordered by date</returns>
    public async Task<IEnumerable<Rehearsal>> GetRehearsalsAsync(DateTime startDate, DateTime endDate)
    {
        return await _rehearsalRepository.GetRehearsalsAsync(startDate, endDate);
    }

    /// <summary>
    /// Gets all rehearsals
    /// </summary>
    /// <returns>Collection of all rehearsals</returns>
    public async Task<IEnumerable<Rehearsal>> GetAllRehearsalsAsync()
    {
        return await _rehearsalRepository.GetAllAsync();
    }

    /// <summary>
    /// Gets upcoming rehearsals
    /// </summary>
    /// <param name="count">The maximum number of upcoming rehearsals to return (default: 10)</param>
    /// <returns>Collection of upcoming rehearsals, ordered by date</returns>
    public async Task<IEnumerable<Rehearsal>> GetUpcomingRehearsalsAsync(int count = 10)
    {
        return await _rehearsalRepository.GetUpcomingRehearsalsAsync(count);
    }

    /// <summary>
    /// Creates a new rehearsal
    /// </summary>
    /// <param name="date">The date of the rehearsal</param>
    /// <param name="location">The location of the rehearsal</param>
    /// <param name="theme">Optional theme for the rehearsal</param>
    /// <returns>The created rehearsal</returns>
    public async Task<Rehearsal> CreateRehearsalAsync(DateTime date, string location, string? theme = null)
    {
        var rehearsal = Rehearsal.Create(date, location, theme);
        return await _rehearsalRepository.AddAsync(rehearsal);
    }

    /// <summary>
    /// Updates an existing rehearsal
    /// </summary>
    /// <param name="id">The ID of the rehearsal to update</param>
    /// <param name="location">The new location</param>
    /// <param name="theme">The new theme (can be null)</param>
    /// <param name="description">The new description (can be null)</param>
    /// <param name="notes">The new notes (can be null)</param>
    /// <exception cref="EntityNotFoundException">Thrown when the rehearsal is not found</exception>
    public async Task UpdateRehearsalAsync(int id, string location, string? theme, string? description, string? notes)
    {
        var rehearsal = await _rehearsalRepository.GetByIdOrThrowAsync(id);

        rehearsal.UpdateDetails(location, theme, description, notes);
        await _rehearsalRepository.UpdateAsync(rehearsal);
    }

    /// <summary>
    /// Cancels a rehearsal and deletes all associated attendances
    /// </summary>
    /// <param name="id">The ID of the rehearsal to cancel</param>
    /// <param name="reason">The reason for cancellation</param>
    /// <exception cref="EntityNotFoundException">Thrown when the rehearsal is not found</exception>
    public async Task CancelRehearsalAsync(int id, string reason)
    {
        var rehearsal = await _rehearsalRepository.GetByIdOrThrowAsync(id);

        rehearsal.Cancel(reason);
        await _rehearsalRepository.UpdateAsync(rehearsal);

        // Delete all attendances for this rehearsal using batch operation
        await _attendanceRepository.DeleteByRehearsalIdAsync(id);
        rehearsal.Attendances.Clear();
    }

    /// <summary>
    /// Uncancels a previously cancelled rehearsal
    /// </summary>
    /// <param name="id">The ID of the rehearsal to uncancel</param>
    /// <exception cref="EntityNotFoundException">Thrown when the rehearsal is not found</exception>
    public async Task UncancelRehearsalAsync(int id)
    {
        var rehearsal = await _rehearsalRepository.GetByIdOrThrowAsync(id);

        rehearsal.Uncancel();
        await _rehearsalRepository.UpdateAsync(rehearsal);
    }

    /// <summary>
    /// Deletes a rehearsal
    /// </summary>
    /// <param name="id">The ID of the rehearsal to delete</param>
    /// <exception cref="EntityNotFoundException">Thrown when the rehearsal is not found</exception>
    public async Task DeleteRehearsalAsync(int id)
    {
        var rehearsal = await _rehearsalRepository.GetByIdOrThrowAsync(id);

        await _rehearsalRepository.DeleteAsync(rehearsal);
    }
}
