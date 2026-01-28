using System.Web;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing URL state for events page
/// Extracted from Events.razor to improve separation of concerns
/// </summary>
public class EventUrlService : IEventUrlService
{
    public string BuildQueryString(string? selectedFiscalYear, string? currentFiscalYear, string? selectedEventType)
    {
        var queryParams = new List<string>();

        // Add fiscal year if not default
        if (!string.IsNullOrEmpty(selectedFiscalYear) && selectedFiscalYear != currentFiscalYear)
        {
            queryParams.Add($"fy={Uri.EscapeDataString(selectedFiscalYear)}");
        }

        // Add event type if selected
        if (!string.IsNullOrEmpty(selectedEventType))
        {
            queryParams.Add($"type={Uri.EscapeDataString(selectedEventType)}");
        }

        return queryParams.Any() ? string.Join("&", queryParams) : "";
    }

    public (string? fiscalYear, string? eventType) ParseQueryString(string? queryString)
    {
        if (string.IsNullOrEmpty(queryString))
            return (null, null);

        // Remove leading ? if present
        if (queryString.StartsWith("?"))
            queryString = queryString.Substring(1);

        var queryParams = HttpUtility.ParseQueryString(queryString);
        var fiscalYear = queryParams["fy"];
        var eventType = queryParams["type"];

        return (fiscalYear, eventType);
    }
}
