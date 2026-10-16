using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for Event entity with domain-specific operations
/// Extends generic repository with event-specific queries
/// </summary>
public interface IEventRepository : IRepository<Event>
{

    /// <summary>
    /// Gets events by type
    /// </summary>
    /// <param name="type">Event type filter</param>
    /// <returns>Collection of events matching the type</returns>
    Task<IEnumerable<Event>> GetEventsByTypeAsync(EventType type);
}
