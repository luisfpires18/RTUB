using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for filtering events
/// Extracted from Events.razor to improve separation of concerns
/// </summary>
public interface IEventFilterService
{
    /// <summary>
    /// Filters events by fiscal year and event type, then splits into future and past events
    /// </summary>
    /// <param name="allEvents">All events to filter</param>
    /// <param name="selectedFiscalYear">Selected fiscal year filter (format: "YYYY-YYYY")</param>
    /// <param name="selectedEventType">Selected event type filter</param>
    /// <param name="previousEventsSearch">Search term for previous events (searches Name, Location, Description)</param>
    /// <returns>Tuple of (futureEvents, previousEvents)</returns>
    (List<Event> futureEvents, List<Event> previousEvents) FilterEvents(
        IEnumerable<Event> allEvents,
        string? selectedFiscalYear,
        string? selectedEventType,
        string? previousEventsSearch);
}
