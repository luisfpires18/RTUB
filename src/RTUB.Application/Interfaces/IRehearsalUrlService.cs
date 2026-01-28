namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for managing URL state for rehearsals page
/// Extracted from Rehearsals.razor to improve separation of concerns
/// </summary>
public interface IRehearsalUrlService
{
    /// <summary>
    /// Builds the URL query string from current filter state
    /// </summary>
    /// <param name="selectedFiscalYear">Currently selected fiscal year</param>
    /// <param name="currentFiscalYear">Current fiscal year</param>
    /// <param name="searchTerm">Search term for upcoming rehearsals</param>
    /// <param name="previousRehearsalsSearch">Search term for previous rehearsals</param>
    /// <returns>Query string (without leading ?) or empty string if no filters</returns>
    string BuildQueryString(string selectedFiscalYear, string currentFiscalYear, string searchTerm, string previousRehearsalsSearch);
}
