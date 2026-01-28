using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for filtering, sorting, and paginating transactions
/// Extracted from Report.razor to improve separation of concerns
/// </summary>
public interface ITransactionFilterService
{
    /// <summary>
    /// Filters, sorts, and paginates transactions for a specific activity
    /// </summary>
    /// <param name="transactions">Collection of transactions to filter</param>
    /// <param name="searchTerm">Optional search term to filter by description or category</param>
    /// <param name="sortColumn">Column to sort by (Date, Description, Category, Type, Amount)</param>
    /// <param name="sortDescending">Whether to sort in descending order</param>
    /// <param name="currentPage">Current page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <returns>Filtered, sorted, and paginated list of transactions</returns>
    List<Transaction> GetFilteredSortedPaginatedTransactions(
        IEnumerable<Transaction> transactions,
        string? searchTerm,
        string sortColumn,
        bool sortDescending,
        int currentPage,
        int pageSize);
}
