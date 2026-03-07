using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for Event operations
/// Abstracts business logic from presentation layer
/// </summary>
public interface IEventService
{
    Task<Event?> GetEventByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Event>> GetAllEventsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Event>> GetUpcomingEventsAsync(int count = 10, CancellationToken cancellationToken = default);
    Task<IEnumerable<Event>> GetPastEventsAsync(int count = 10, CancellationToken cancellationToken = default);
    Task<IEnumerable<Event>> GetEventsByTypeAsync(EventType type, CancellationToken cancellationToken = default);
    Task<IEnumerable<Event>> GetActiveEventsByTypeAsync(EventType type, CancellationToken cancellationToken = default);
    Task<Event> CreateEventAsync(string name, DateTime date, string location, EventType type, string description = "", DateTime? endDate = null, string? imageUrl = null, CancellationToken cancellationToken = default);
    Task UpdateEventAsync(int id, string name, DateTime date, string location, string description, EventType type, DateTime? endDate = null, CancellationToken cancellationToken = default);
    Task UpdateEventWithImageAsync(int id, string name, DateTime date, string location, string description, EventType type, DateTime? endDate, Stream imageStream, string fileName, string contentType, CancellationToken cancellationToken = default);
    Task SetEventImageAsync(int id, Stream imageStream, string fileName, string contentType, CancellationToken cancellationToken = default);
    Task DeleteEventAsync(int id, CancellationToken cancellationToken = default);
    Task CancelEventAsync(int id, string reason, CancellationToken cancellationToken = default);
    Task UncancelEventAsync(int id, CancellationToken cancellationToken = default);
    Task<IEnumerable<EventVideo>> GetVideosByEventIdAsync(int eventId, CancellationToken cancellationToken = default);
    Task<EventVideo> AddVideoAsync(int eventId, Stream fileStream, string fileName, string contentType, string createdByUserId, string? title = null, CancellationToken cancellationToken = default);
    Task UpdateVideoTitleAsync(int videoId, string? title, string userId, bool isAdmin = false, CancellationToken cancellationToken = default);
    Task UpdateVideoOrderAsync(int eventId, List<int> videoIds, CancellationToken cancellationToken = default);
    Task DeleteVideoAsync(int videoId, string userId, bool isAdmin = false, CancellationToken cancellationToken = default);
    Task<int> GetVideoCountByEventIdAsync(int eventId, CancellationToken cancellationToken = default);
}
