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
    /// Gets upcoming events (events with end date or start date >= today)
    /// </summary>
    /// <param name="count">Maximum number of events to return</param>
    /// <returns>Collection of upcoming events ordered by date</returns>
    Task<IEnumerable<Event>> GetUpcomingEventsAsync(int count = 10);

    /// <summary>
    /// Gets past events (events with end date or start date < today)
    /// </summary>
    /// <param name="count">Maximum number of events to return</param>
    /// <returns>Collection of past events ordered by date descending</returns>
    Task<IEnumerable<Event>> GetPastEventsAsync(int count = 10);

    /// <summary>
    /// Gets events by type
    /// </summary>
    /// <param name="type">Event type filter</param>
    /// <returns>Collection of events matching the type</returns>
    Task<IEnumerable<Event>> GetEventsByTypeAsync(EventType type);

    /// <summary>
    /// Gets event with its repertoire
    /// </summary>
    /// <param name="id">Event ID</param>
    /// <returns>Event with repertoire loaded, or null if not found</returns>
    Task<Event?> GetEventWithRepertoireAsync(int id);
}
