using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for PostMedia entity
/// </summary>
public interface IPostMediaRepository : IRepository<PostMedia>
{
    /// <summary>
    /// Gets media items for a specific post
    /// </summary>
    Task<IEnumerable<PostMedia>> GetByPostIdAsync(int postId);

    /// <summary>
    /// Deletes all media items for a specific post
    /// </summary>
    Task DeleteByPostIdAsync(int postId);
}
