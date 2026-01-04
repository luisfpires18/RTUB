using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for LoginCount operations
/// </summary>
public interface ILoginCountService
{
    /// <summary>
    /// Gets the login count for a user on a specific date
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="date">The date to query</param>
    /// <returns>The login count or 0 if not found</returns>
    Task<int> GetLoginCountForDateAsync(string userId, DateTime date);

    /// <summary>
    /// Gets all login counts for a user
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <returns>List of LoginCount entities ordered by date descending</returns>
    Task<List<LoginCount>> GetLoginHistoryAsync(string userId);

    /// <summary>
    /// Gets login counts for a user within a date range
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="startDate">Start date (inclusive)</param>
    /// <param name="endDate">End date (inclusive)</param>
    /// <returns>List of LoginCount entities ordered by date descending</returns>
    Task<List<LoginCount>> GetLoginHistoryForDateRangeAsync(string userId, DateTime startDate, DateTime endDate);

    /// <summary>
    /// Gets the total number of logins for a user across all time
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <returns>Total login count</returns>
    Task<int> GetTotalLoginCountAsync(string userId);
}
