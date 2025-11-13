using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for Post operations
/// </summary>
public interface IPostService
{
    Task<Post?> GetPostByIdAsync(int id);
    Task<IEnumerable<Post>> GetPostsByDiscussionIdAsync(int discussionId);
    Task<IEnumerable<Post>> GetPostsByUserIdAsync(string userId);
    Task<Post> CreatePostAsync(int discussionId, string userId, string content);
    Task UpdatePostAsync(int id, string content);
    Task DeletePostAsync(int id);
}
