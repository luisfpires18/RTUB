using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for Transaction entity with domain-specific operations
/// </summary>
public interface ITransactionRepository : IRepository<Transaction>
{
    /// <summary>
    /// Gets transactions by activity ID
    /// </summary>
    Task<IEnumerable<Transaction>> GetTransactionsByActivityIdAsync(int activityId);

    /// <summary>
    /// Gets transactions for multiple activity IDs (batch operation to avoid N+1 queries)
    /// </summary>
    Task<IEnumerable<Transaction>> GetTransactionsByActivityIdsAsync(IEnumerable<int> activityIds);

    /// <summary>
    /// Gets transactions by type (income/expense)
    /// </summary>
    Task<IEnumerable<Transaction>> GetTransactionsByTypeAsync(string type);

    /// <summary>
    /// Gets transactions by user ID (for CALOTES tracking)
    /// </summary>
    Task<IEnumerable<Transaction>> GetTransactionsByUserIdAsync(string userId);
}
