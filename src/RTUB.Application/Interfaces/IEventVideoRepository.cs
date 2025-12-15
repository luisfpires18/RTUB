using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for EventVideo entity
/// </summary>
public interface IEventVideoRepository : IRepository<EventVideo>
{
    /// <summary>
    /// Gets video items for a specific event
    /// </summary>
    Task<IEnumerable<EventVideo>> GetByEventIdAsync(int eventId);

    /// <summary>
    /// Gets the count of videos for a specific event
    /// </summary>
    Task<int> GetCountByEventIdAsync(int eventId);
}
