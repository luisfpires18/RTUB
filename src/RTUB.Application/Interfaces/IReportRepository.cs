using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for Report entity
/// Provides data access operations for reports
/// </summary>
public interface IReportRepository : IRepository<Report>
{
    /// <summary>
    /// Gets all published reports with activities and transactions
    /// </summary>
    Task<IEnumerable<Report>> GetPublishedWithActivitiesAsync();

    /// <summary>
    /// Gets report by ID with activities and transactions
    /// </summary>
    Task<Report?> GetByIdWithActivitiesAsync(int id);

    /// <summary>
    /// Gets all reports with activities and transactions
    /// </summary>
    Task<IEnumerable<Report>> GetAllWithActivitiesAsync();
}
