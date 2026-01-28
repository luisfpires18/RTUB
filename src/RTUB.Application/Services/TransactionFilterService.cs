using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Service for filtering, sorting, and paginating transactions
/// Extracted from Report.razor to improve separation of concerns
/// </summary>
public class TransactionFilterService : ITransactionFilterService
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
    public List<Transaction> GetFilteredSortedPaginatedTransactions(
        IEnumerable<Transaction> transactions,
        string? searchTerm,
        string sortColumn,
        bool sortDescending,
        int currentPage,
        int pageSize)
    {
        var result = transactions.ToList();

        // Filter
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var search = searchTerm.ToLower();
            result = result
                .Where(t =>
                    (t.Description?.ToLower().Contains(search) ?? false) ||
                    (t.Category?.ToLower().Contains(search) ?? false))
                .ToList();
        }

        // Sort
        result = sortColumn switch
        {
            "Date" => sortDescending
                ? result.OrderByDescending(t => t.Date).ToList()
                : result.OrderBy(t => t.Date).ToList(),
            "Description" => sortDescending
                ? result.OrderByDescending(t => t.Description).ToList()
                : result.OrderBy(t => t.Description).ToList(),
            "Category" => sortDescending
                ? result.OrderByDescending(t => t.Category).ToList()
                : result.OrderBy(t => t.Category).ToList(),
            "Type" => sortDescending
                ? result.OrderByDescending(t => t.Type).ToList()
                : result.OrderBy(t => t.Type).ToList(),
            "Amount" => sortDescending
                ? result.OrderByDescending(t => t.Amount).ToList()
                : result.OrderBy(t => t.Amount).ToList(),
            _ => result.OrderByDescending(t => t.Date).ToList()
        };

        // Paginate
        return result
            .Skip((currentPage - 1) * pageSize)
            .Take(pageSize)
            .ToList();
    }
}
