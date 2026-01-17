using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for NaipeComment entity
/// Provides data access operations for naipe comments
/// </summary>
public interface INaipeCommentRepository : IRepository<NaipeComment>
{
    /// <summary>
    /// Gets comments for a specific content with author details
    /// </summary>
    Task<IEnumerable<NaipeComment>> GetCommentsForContentAsync(int contentId);

    /// <summary>
    /// Gets comment by ID with author details
    /// </summary>
    Task<NaipeComment?> GetByIdWithDetailsAsync(int id);
}
