using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for EventRepertoire entity
/// </summary>
public interface IEventRepertoireRepository : IRepository<EventRepertoire>
{
    /// <summary>
    /// Get repertoire items for an event ordered by display order
    /// </summary>
    Task<IEnumerable<EventRepertoire>> GetRepertoireByEventIdAsync(int eventId);
    
    /// <summary>
    /// Check if a song already exists in an event's repertoire
    /// </summary>
    Task<bool> SongExistsInRepertoireAsync(int eventId, int songId);
    
    /// <summary>
    /// Get repertoire item with song and event details
    /// </summary>
    Task<EventRepertoire?> GetRepertoireItemWithDetailsAsync(int id);
}
