using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for trophy management
/// </summary>
public interface ITrophyService
{
    Task<Trophy?> GetByIdAsync(int id);
    Task<IEnumerable<Trophy>> GetAllAsync();
    Task<IEnumerable<Trophy>> GetByEventIdAsync(int eventId);
    Task<Trophy> CreateAsync(Trophy trophy);
    Task UpdateAsync(Trophy trophy);
    Task DeleteAsync(int id);
}
