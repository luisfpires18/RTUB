using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for Post operations
/// </summary>
public interface IPostService
{
    Task<Post?> GetByIdAsync(int id);
    Task<IEnumerable<Post>> GetByDiscussionIdAsync(int discussionId, int page = 1, int pageSize = 20, string? searchTerm = null);
    Task<int> GetCountByDiscussionIdAsync(int discussionId, string? searchTerm = null);
    Task<Post> CreateAsync(int discussionId, string authorId, string title, string body, string? mentionsJson = null);
    Task UpdateAsync(int id, string title, string body, string? mentionsJson = null);
    Task PinAsync(int id);
    Task UnpinAsync(int id);
    Task LockAsync(int id);
    Task UnlockAsync(int id);
    Task SoftDeleteAsync(int id);
    Task UpdateLastActivityAsync(int id);
}
