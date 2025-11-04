using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for rehearsal management
/// </summary>
public interface IRehearsalService
{
    // Rehearsal CRUD
    Task<Rehearsal?> GetRehearsalByIdAsync(int id);
    Task<Rehearsal?> GetRehearsalByDateAsync(DateTime date);
    Task<IEnumerable<Rehearsal>> GetRehearsalsAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<Rehearsal>> GetUpcomingRehearsalsAsync(int count = 10);
    Task<Rehearsal> CreateRehearsalAsync(DateTime date, string location, string? theme = null);
    Task UpdateRehearsalAsync(int id, string location, string? theme, string? notes);
    Task CancelRehearsalAsync(int id);
    Task DeleteRehearsalAsync(int id);
}
