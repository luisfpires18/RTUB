using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// Service for calculating event statistics (trophy statistics)
/// Extracted from Events.razor to improve separation of concerns
/// </summary>
public class EventStatisticsService : IEventStatisticsService
{
    private readonly IEventService _eventService;
    private readonly ITrophyService _trophyService;

    /// <summary>
    /// Initializes a new instance of the EventStatisticsService
    /// </summary>
    /// <param name="eventService">Service for event operations</param>
    /// <param name="trophyService">Service for trophy operations</param>
    public EventStatisticsService(
        IEventService eventService,
        ITrophyService trophyService)
    {
        _eventService = eventService ?? throw new ArgumentNullException(nameof(eventService));
        _trophyService = trophyService ?? throw new ArgumentNullException(nameof(trophyService));
    }

    /// <summary>
    /// Gets trophy statistics grouped by event for all past festival events
    /// </summary>
    /// <param name="maxEvents">Maximum number of past events to include (default: 100)</param>
    /// <returns>List of trophy statistics grouped by event, ordered by trophy count descending, then by event date descending</returns>
    public async Task<List<TrophyStatsByEvent>> GetTrophyStatisticsByEventAsync(int maxEvents = 100)
    {
        // Get all past events
        var pastEvents = (await _eventService.GetPastEventsAsync(maxEvents)).ToList();

        // Get all trophies
        var allTrophies = (await _trophyService.GetAllAsync()).ToList();

        // Group trophies by event
        var trophiesByEvent = allTrophies
            .Where(t => t.EventId > 0)
            .GroupBy(t => t.EventId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Create stats for all past events (only festivals can have trophies)
        var groupedByEvent = pastEvents
            .Where(evt => evt.Type == EventType.Festival)
            .Select(evt =>
            {
                var eventTrophies = trophiesByEvent.TryGetValue(evt.Id, out var trophyList) ? trophyList : new List<Trophy>();
                return new TrophyStatsByEvent
                {
                    EventId = evt.Id,
                    EventName = evt.Name ?? "Evento desconhecido",
                    EventDate = evt.Date,
                    TrophyCount = eventTrophies.Count,
                    Trophies = eventTrophies
                };
            })
            .OrderByDescending(s => s.TrophyCount)
            .ThenByDescending(s => s.EventDate)
            .ToList();

        return groupedByEvent;
    }
}
