using Microsoft.AspNetCore.Components.Forms;
using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for Comment operations
/// </summary>
public interface ICommentService
{
    Task<Comment?> GetByIdAsync(int id);
    Task<IEnumerable<Comment>> GetByPostIdAsync(int postId, int page = 1, int pageSize = 50);
    Task<int> GetCountByPostIdAsync(int postId);
    Task<Dictionary<int, int>> GetCountsByPostIdsAsync(IEnumerable<int> postIds);
    Task<Comment> CreateAsync(int postId, string authorId, string body, string? mentionsJson = null, IReadOnlyList<IBrowserFile>? imageFiles = null);
    Task UpdateAsync(int id, string body, string? mentionsJson = null);
    Task SoftDeleteAsync(int id);
}
