using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for Meeting entity with domain-specific operations
/// </summary>
public interface IMeetingRepository : IRepository<Meeting>
{
    /// <summary>
    /// Gets meetings with pagination, search, and Veterano filtering
    /// </summary>
    Task<IEnumerable<Meeting>> GetAllMeetingsAsync(string? searchTerm, int pageNumber, int pageSize, string userId);

    /// <summary>
    /// Gets meeting by ID with Veterano visibility check
    /// </summary>
    Task<Meeting?> GetMeetingByIdAsync(int id, string userId);

    /// <summary>
    /// Gets total count of meetings with search and Veterano filtering
    /// </summary>
    Task<int> GetTotalCountAsync(string? searchTerm, string userId);
}
