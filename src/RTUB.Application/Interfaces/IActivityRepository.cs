using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for Activity entity
/// Provides data access operations for activities
/// </summary>
public interface IActivityRepository : IRepository<Activity>
{
    /// <summary>
    /// Gets activity with related report
    /// </summary>
    Task<Activity?> GetWithReportAsync(int id);
    
    /// <summary>
    /// Gets activity with transactions
    /// </summary>
    Task<Activity?> GetWithTransactionsAsync(int id);
}
