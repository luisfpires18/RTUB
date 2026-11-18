using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for Trophy entity
/// Provides data access operations for trophies
/// </summary>
public interface ITrophyRepository : IRepository<Trophy>
{
    /// <summary>
    /// Gets trophies by event ID
    /// </summary>
    Task<IEnumerable<Trophy>> GetByEventIdAsync(int eventId);

}
