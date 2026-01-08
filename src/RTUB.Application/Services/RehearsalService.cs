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

    public RehearsalService(IRehearsalRepository rehearsalRepository, IRehearsalAttendanceRepository attendanceRepository)
    {
        _rehearsalRepository = rehearsalRepository;
        _attendanceRepository = attendanceRepository;
    }

    public async Task<Rehearsal?> GetRehearsalByIdAsync(int id)
    {
        return await _rehearsalRepository.GetByIdAsync(id);
    }

    public async Task<Rehearsal?> GetRehearsalByDateAsync(DateTime date)
    {
        return await _rehearsalRepository.GetRehearsalByDateAsync(date);
    }

    public async Task<IEnumerable<Rehearsal>> GetRehearsalsAsync(DateTime startDate, DateTime endDate)
    {
        return await _rehearsalRepository.GetRehearsalsAsync(startDate, endDate);
    }

    public async Task<IEnumerable<Rehearsal>> GetAllRehearsalsAsync()
    {
        return await _rehearsalRepository.GetAllAsync();
    }

    public async Task<IEnumerable<Rehearsal>> GetUpcomingRehearsalsAsync(int count = 10)
    {
        return await _rehearsalRepository.GetUpcomingRehearsalsAsync(count);
    }

    public async Task<Rehearsal> CreateRehearsalAsync(DateTime date, string location, string? theme = null)
    {
        var rehearsal = Rehearsal.Create(date, location, theme);
        return await _rehearsalRepository.AddAsync(rehearsal);
    }

    public async Task UpdateRehearsalAsync(int id, string location, string? theme, string? notes)
    {
        var rehearsal = await _rehearsalRepository.GetByIdOrThrowAsync(id);

        rehearsal.UpdateDetails(location, theme, notes);
        await _rehearsalRepository.UpdateAsync(rehearsal);
    }

    public async Task CancelRehearsalAsync(int id, string reason)
    {
        var rehearsal = await _rehearsalRepository.GetByIdOrThrowAsync(id);

        rehearsal.Cancel(reason);
        await _rehearsalRepository.UpdateAsync(rehearsal);

        // Delete all attendances for this rehearsal using batch operation
        await _attendanceRepository.DeleteByRehearsalIdAsync(id);
        rehearsal.Attendances.Clear();
    }

    public async Task UncancelRehearsalAsync(int id)
    {
        var rehearsal = await _rehearsalRepository.GetByIdOrThrowAsync(id);

        rehearsal.Uncancel();
        await _rehearsalRepository.UpdateAsync(rehearsal);
    }

    public async Task DeleteRehearsalAsync(int id)
    {
        var rehearsal = await _rehearsalRepository.GetByIdOrThrowAsync(id);

        await _rehearsalRepository.DeleteAsync(rehearsal);
    }
}
