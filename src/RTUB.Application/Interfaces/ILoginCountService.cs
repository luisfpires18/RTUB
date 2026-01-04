using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for managing login count records
/// </summary>
public interface ILoginCountService
{
    /// <summary>
    /// Records a new login event
    /// </summary>
    Task RecordLoginAsync(string userId, string? ipAddress = null, string? userAgent = null);

    /// <summary>
    /// Gets all login records for a specific user
    /// </summary>
    Task<IEnumerable<LoginCount>> GetUserLoginsAsync(string userId);

    /// <summary>
    /// Gets total login count for a specific user
    /// </summary>
    Task<int> GetUserLoginCountAsync(string userId);

    /// <summary>
    /// Gets login records with pagination and filtering
    /// </summary>
    Task<(IEnumerable<LoginCount> logins, int totalCount)> GetPagedLoginsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        int page = 1,
        int pageSize = 20);

    /// <summary>
    /// Gets top users by login count
    /// </summary>
    Task<IEnumerable<(ApplicationUser User, int LoginCount)>> GetTopUsersByLoginCountAsync(int top = 10);

    /// <summary>
    /// Gets consecutive login days for a specific user
    /// </summary>
    Task<int> GetConsecutiveLoginDaysAsync(string userId);

    /// <summary>
    /// Gets total unique users who have logged in
    /// </summary>
    Task<int> GetTotalUniqueUsersAsync();

    /// <summary>
    /// Gets login statistics for dashboard
    /// </summary>
    Task<LoginStatistics> GetLoginStatisticsAsync();
}

/// <summary>
/// Statistics for login tracking dashboard
/// </summary>
public class LoginStatistics
{
    public int TotalLogins { get; set; }
    public int TotalUniqueUsers { get; set; }
    public int LoginsToday { get; set; }
    public int LoginsThisWeek { get; set; }
    public int LoginsThisMonth { get; set; }
}
