using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing URL state for rehearsals page
/// Extracted from Rehearsals.razor to improve separation of concerns
/// </summary>
public class RehearsalUrlService : IRehearsalUrlService
{
    /// <summary>
    /// Builds the URL query string from current filter state
    /// </summary>
    /// <param name="selectedFiscalYear">Currently selected fiscal year</param>
    /// <param name="currentFiscalYear">Current fiscal year</param>
    /// <param name="searchTerm">Search term for upcoming rehearsals</param>
    /// <param name="previousRehearsalsSearch">Search term for previous rehearsals</param>
    /// <returns>Query string (without leading ?) or empty string if no filters</returns>
    public string BuildQueryString(string selectedFiscalYear, string currentFiscalYear, string searchTerm, string previousRehearsalsSearch)
    {
        var queryParams = new List<string>();

        // Add fiscal year if not default
        if (!string.IsNullOrEmpty(selectedFiscalYear) && selectedFiscalYear != currentFiscalYear)
        {
            queryParams.Add($"fy={Uri.EscapeDataString(selectedFiscalYear)}");
        }

        // Add search terms if not empty
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            queryParams.Add($"qNext={Uri.EscapeDataString(searchTerm)}");
        }

        if (!string.IsNullOrWhiteSpace(previousRehearsalsSearch))
        {
            queryParams.Add($"qPrev={Uri.EscapeDataString(previousRehearsalsSearch)}");
        }

        return queryParams.Any() ? string.Join("&", queryParams) : "";
    }
}
