using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for Comment operations
/// </summary>
public interface ICommentService
{
    Task<Comment?> GetCommentByIdAsync(int id);
    Task<IEnumerable<Comment>> GetCommentsByPostIdAsync(int postId);
    Task<IEnumerable<Comment>> GetCommentsByUserIdAsync(string userId);
    Task<Comment> CreateCommentAsync(int postId, string userId, string content);
    Task UpdateCommentAsync(int id, string content);
    Task DeleteCommentAsync(int id);
}
