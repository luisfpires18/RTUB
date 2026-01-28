using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// Service for filtering events
/// Extracted from Events.razor to improve separation of concerns
/// </summary>
public class EventFilterService : IEventFilterService
{
    public (List<Event> futureEvents, List<Event> previousEvents) FilterEvents(
        IEnumerable<Event> allEvents,
        string? selectedFiscalYear,
        string? selectedEventType,
        string? previousEventsSearch)
    {
        if (allEvents == null)
        {
            return (new List<Event>(), new List<Event>());
        }

        // Determine the date range based on selected fiscal year
        DateTime? fiscalYearStart = null;
        DateTime? fiscalYearEnd = null;

        if (!string.IsNullOrEmpty(selectedFiscalYear))
        {
            var parts = selectedFiscalYear.Split('-');
            if (parts.Length == 2 && int.TryParse(parts[0], out int startYear))
            {
                fiscalYearStart = new DateTime(startYear, 9, 1); // September 1st
                fiscalYearEnd = new DateTime(startYear + 1, 8, 31); // August 31st next year
            }
        }

        // Apply fiscal year and event type filters
        var filtered = allEvents.AsEnumerable();

        // Filter by fiscal year
        if (fiscalYearStart.HasValue && fiscalYearEnd.HasValue)
        {
            filtered = filtered.Where(e => e.Date >= fiscalYearStart.Value && e.Date <= fiscalYearEnd.Value);
        }

        // Filter by event type
        if (!string.IsNullOrEmpty(selectedEventType) && Enum.TryParse<EventType>(selectedEventType, out var eventTypeFilter))
        {
            filtered = filtered.Where(e => e.Type == eventTypeFilter);
        }

        var filteredList = filtered.ToList();

        // Split into future and past events
        var today = DateTime.Today;
        var futureEvents = filteredList
            .Where(e => e.Date >= today)
            .OrderBy(e => e.Date)
            .ToList();

        // Filter previous events (also apply search)
        var previousFiltered = filteredList.Where(e => e.Date < today);

        // Apply search filter to previous events
        if (!string.IsNullOrWhiteSpace(previousEventsSearch))
        {
            var searchLower = previousEventsSearch.ToLower();
            previousFiltered = previousFiltered.Where(e =>
                (e.Name?.ToLower().Contains(searchLower) ?? false) ||
                (e.Location?.ToLower().Contains(searchLower) ?? false) ||
                (e.Description?.ToLower().Contains(searchLower) ?? false));
        }

        var previousEvents = previousFiltered
            .OrderByDescending(e => e.Date)
            .ToList();

        return (futureEvents, previousEvents);
    }
}
