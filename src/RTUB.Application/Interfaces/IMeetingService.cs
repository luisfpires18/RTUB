using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for meeting management
/// </summary>
public interface IMeetingService
{
    /// <summary>
    /// Gets all meetings with pagination and visibility filtering
    /// </summary>
    /// <param name="searchTerm">Optional search term for title and statement</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="userId">Current user ID for visibility filtering</param>
    /// <returns>Paginated list of meetings</returns>
    Task<IEnumerable<Meeting>> GetAllMeetingsAsync(string? searchTerm, int pageNumber, int pageSize, string userId);
    
    /// <summary>
    /// Gets a meeting by ID if user has permission to view it
    /// </summary>
    /// <param name="id">Meeting ID</param>
    /// <param name="userId">Current user ID for visibility check</param>
    /// <returns>Meeting if found and user has permission, null otherwise</returns>
    Task<Meeting?> GetMeetingByIdAsync(int id, string userId);
    
    /// <summary>
    /// Creates a new meeting
    /// </summary>
    /// <param name="meeting">Meeting to create</param>
    /// <returns>Created meeting</returns>
    Task<Meeting> CreateMeetingAsync(Meeting meeting);
    
    /// <summary>
    /// Updates an existing meeting
    /// </summary>
    /// <param name="meeting">Meeting with updated data</param>
    /// <returns>Task</returns>
    Task UpdateMeetingAsync(Meeting meeting);
    
    /// <summary>
    /// Deletes a meeting
    /// </summary>
    /// <param name="id">Meeting ID to delete</param>
    /// <returns>Task</returns>
    Task DeleteMeetingAsync(int id);
    
    /// <summary>
    /// Gets total count of meetings with visibility filtering
    /// </summary>
    /// <param name="searchTerm">Optional search term</param>
    /// <param name="userId">Current user ID for visibility filtering</param>
    /// <returns>Total count</returns>
    Task<int> GetTotalCountAsync(string? searchTerm, string userId);
}
