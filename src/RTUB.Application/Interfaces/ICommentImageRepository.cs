using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for CommentImage entity
/// </summary>
public interface ICommentImageRepository : IRepository<CommentImage>
{
    /// <summary>
    /// Gets images for a specific comment
    /// </summary>
    Task<IEnumerable<CommentImage>> GetByCommentIdAsync(int commentId);

    /// <summary>
    /// Deletes all images for a specific comment
    /// </summary>
    Task DeleteByCommentIdAsync(int commentId);
}
