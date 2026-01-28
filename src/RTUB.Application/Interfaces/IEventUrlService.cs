namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for managing URL state for events page
/// Extracted from Events.razor to improve separation of concerns
/// </summary>
public interface IEventUrlService
{
    /// <summary>
    /// Builds the query string for events page based on filters
    /// </summary>
    /// <param name="selectedFiscalYear">Selected fiscal year filter</param>
    /// <param name="currentFiscalYear">Current fiscal year (to determine if selected is default)</param>
    /// <param name="selectedEventType">Selected event type filter</param>
    /// <returns>Query string (without leading ?)</returns>
    string BuildQueryString(string? selectedFiscalYear, string? currentFiscalYear, string? selectedEventType);

    /// <summary>
    /// Parses query parameters from URL
    /// </summary>
    /// <param name="queryString">Query string (with or without leading ?)</param>
    /// <returns>Tuple of (fiscalYear, eventType)</returns>
    (string? fiscalYear, string? eventType) ParseQueryString(string? queryString);
}
