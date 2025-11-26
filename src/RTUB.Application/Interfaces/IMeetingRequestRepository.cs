using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for MeetingRequest entity
/// Provides data access operations for CV meeting requests
/// </summary>
public interface IMeetingRequestRepository : IRepository<MeetingRequest>
{
    /// <summary>
    /// Gets meeting requests with author, optionally filtered by status
    /// </summary>
    Task<IEnumerable<MeetingRequest>> GetAllWithAuthorAsync(RequestStatus? status = null);

    /// <summary>
    /// Gets paginated meeting requests with author
    /// </summary>
    Task<IEnumerable<MeetingRequest>> GetPagedWithAuthorAsync(int page, int pageSize, RequestStatus? status = null);

    /// <summary>
    /// Gets meeting request by ID with author
    /// </summary>
    Task<MeetingRequest?> GetByIdWithAuthorAsync(int id);

    /// <summary>
    /// Gets total count of meeting requests by status
    /// </summary>
    Task<int> GetCountAsync(RequestStatus? status = null);
}
