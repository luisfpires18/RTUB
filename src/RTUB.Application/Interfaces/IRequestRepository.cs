using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for Request entity
/// Provides data access operations for requests
/// </summary>
public interface IRequestRepository : IRepository<Request>
{
    /// <summary>
    /// Gets requests by status ordered by preferred date
    /// </summary>
    Task<IEnumerable<Request>> GetByStatusAsync(RequestStatus status);

    /// <summary>
    /// Gets count of pending requests
    /// </summary>
    Task<int> GetPendingCountAsync();
}
