using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for Request entity
/// Provides data access operations for requests
/// </summary>
public interface IRequestRepository : IRepository<Request>
{
    /// <summary>
    /// Gets count of pending requests
    /// </summary>
    Task<int> GetPendingCountAsync();
}
