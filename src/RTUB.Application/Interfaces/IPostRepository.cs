using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for Post entity with domain-specific operations
/// </summary>
public interface IPostRepository : IRepository<Post>
{
    /// <summary>
    /// Gets posts by discussion ID with pagination and search
    /// </summary>
    Task<IEnumerable<Post>> GetByDiscussionIdAsync(int discussionId, int page, int pageSize, string? searchTerm);

    /// <summary>
    /// Gets count of posts by discussion ID with optional search
    /// </summary>
    Task<int> GetCountByDiscussionIdAsync(int discussionId, string? searchTerm);
}
