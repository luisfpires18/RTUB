using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for EventRepertoire entity
/// </summary>
public interface IEventRepertoireRepository : IRepository<EventRepertoire>
{
    /// <summary>
    /// Get repertoire items for an event ordered by display order, optionally filtered by date
    /// </summary>
    Task<IEnumerable<EventRepertoire>> GetRepertoireByEventIdAsync(int eventId, DateTime? date = null);
    
    /// <summary>
    /// Check if a song already exists in an event's repertoire for a specific date
    /// </summary>
    Task<bool> SongExistsInRepertoireAsync(int eventId, int songId, DateTime date);
    
    /// <summary>
    /// Get repertoire item with song and event details
    /// </summary>
    Task<EventRepertoire?> GetRepertoireItemWithDetailsAsync(int id);
    
    /// <summary>
    /// Get distinct repertoire dates for an event
    /// </summary>
    Task<IEnumerable<DateTime>> GetRepertoireDatesAsync(int eventId);
    
    /// <summary>
    /// Remove all repertoire items for a specific event and date
    /// </summary>
    Task RemoveRepertoireDayAsync(int eventId, DateTime date);
}
