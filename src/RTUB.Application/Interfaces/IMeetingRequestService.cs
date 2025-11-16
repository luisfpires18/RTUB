using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for CV meeting request management
/// </summary>
public interface IMeetingRequestService
{
    /// <summary>
    /// Gets all meeting requests with optional filtering
    /// </summary>
    /// <param name="status">Optional status filter</param>
    /// <returns>List of all meeting requests</returns>
    Task<IEnumerable<MeetingRequest>> GetAllAsync(RequestStatus? status = null);
    
    /// <summary>
    /// Gets paginated meeting requests with optional filtering
    /// </summary>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="pageSize">Items per page</param>
    /// <param name="status">Optional status filter</param>
    /// <returns>Paginated list of meeting requests</returns>
    Task<IEnumerable<MeetingRequest>> GetPagedAsync(int page, int pageSize, RequestStatus? status = null);
    
    /// <summary>
    /// Gets total count of meeting requests with optional filtering
    /// </summary>
    /// <param name="status">Optional status filter</param>
    /// <returns>Total count</returns>
    Task<int> GetTotalCountAsync(RequestStatus? status = null);
    
    /// <summary>
    /// Gets a meeting request by ID
    /// </summary>
    /// <param name="id">Meeting request ID</param>
    /// <returns>Meeting request if found, null otherwise</returns>
    Task<MeetingRequest?> GetByIdAsync(int id);
    
    /// <summary>
    /// Creates a new meeting request
    /// </summary>
    /// <param name="request">Meeting request to create</param>
    /// <returns>Created meeting request</returns>
    Task<MeetingRequest> CreateAsync(MeetingRequest request);
    
    /// <summary>
    /// Updates the status of a meeting request
    /// </summary>
    /// <param name="id">Meeting request ID</param>
    /// <param name="status">New status</param>
    /// <returns>Task</returns>
    Task UpdateStatusAsync(int id, RequestStatus status);
    
    /// <summary>
    /// Deletes a meeting request
    /// </summary>
    /// <param name="id">Meeting request ID to delete</param>
    /// <returns>Task</returns>
    Task DeleteAsync(int id);
}
