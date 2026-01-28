using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for Comment entity with domain-specific operations
/// </summary>
public interface ICommentRepository : IRepository<Comment>
{
    /// <summary>
    /// Gets comments by post ID with pagination
    /// </summary>
    Task<IEnumerable<Comment>> GetByPostIdAsync(int postId, int page, int pageSize);

    /// <summary>
    /// Gets count of comments by post ID
    /// </summary>
    Task<int> GetCountByPostIdAsync(int postId);

    /// <summary>
    /// Gets counts of comments for multiple post IDs (batch operation to avoid N+1 queries)
    /// </summary>
    Task<Dictionary<int, int>> GetCountsByPostIdsAsync(IEnumerable<int> postIds);
}
