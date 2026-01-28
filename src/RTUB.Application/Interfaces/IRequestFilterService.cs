using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for filtering and paginating requests
/// Extracted from Requests.razor to improve separation of concerns
/// </summary>
public interface IRequestFilterService
{
    /// <summary>
    /// Applies filters and pagination to requests
    /// </summary>
    /// <param name="requests">Collection of requests to filter</param>
    /// <param name="searchTerm">Search term to filter by</param>
    /// <param name="selectedFiscalYear">Selected fiscal year filter (empty string if none)</param>
    /// <param name="selectedStatusFilter">Selected status filter (empty string if none)</param>
    /// <param name="fiscalYearHelper">Helper for fiscal year date range calculations</param>
    /// <param name="getSearchSelectors">Function to get search selectors for filtering</param>
    /// <param name="getSortColumnSelectors">Function to get sort column selectors</param>
    /// <param name="sortHelper">Helper for sorting</param>
    /// <returns>Tuple containing (pendingRequests, answeredRequests)</returns>
    (List<Request> PendingRequests, List<Request> AnsweredRequests) ApplyFiltersAndPagination(
        IEnumerable<Request> requests,
        string searchTerm,
        string selectedFiscalYear,
        string selectedStatusFilter,
        IFiscalYearHelper fiscalYearHelper,
        Func<List<Func<Request, string>>> getSearchSelectors,
        Func<Dictionary<string, Func<Request, IComparable>>> getSortColumnSelectors,
        object sortHelper);
}
