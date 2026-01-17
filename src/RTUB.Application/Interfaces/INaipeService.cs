using RTUB.Application.DTOs;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for managing naipe educational content (videos/images) and comments
/// </summary>
public interface INaipeService
{
    /// <summary>
    /// Get all content for a specific instrument type
    /// </summary>
    Task<List<NaipeContentDto>> GetContentByInstrumentTypeAsync(InstrumentType type);

    /// <summary>
    /// Get all content across all instrument types
    /// </summary>
    Task<List<NaipeContentDto>> GetAllContentAsync();

    /// <summary>
    /// Get specific content by ID
    /// </summary>
    Task<NaipeContentDto?> GetContentByIdAsync(int id);

    /// <summary>
    /// Create new content (video or image) with file upload
    /// </summary>
    Task<NaipeContentDto> CreateContentAsync(InstrumentType type, string title, string? description, Stream fileStream, string fileName, string mimeType, bool isVideo, decimal sortOrder, string userId);

    /// <summary>
    /// Update existing content
    /// </summary>
    Task UpdateContentAsync(int id, string title, string? description, decimal sortOrder);

    /// <summary>
    /// Delete content
    /// </summary>
    Task DeleteContentAsync(int id);

    /// <summary>
    /// Increment play/view count for content
    /// </summary>
    Task IncrementPlayCountAsync(int contentId, string? userId);

    /// <summary>
    /// Get play count for specific content
    /// </summary>
    Task<int> GetPlayCountAsync(int contentId);

    /// <summary>
    /// Get all comments for a specific content
    /// </summary>
    Task<List<NaipeCommentDto>> GetCommentsAsync(int contentId, string? currentUserId);

    /// <summary>
    /// Add a new comment to content
    /// </summary>
    Task<NaipeCommentDto> AddCommentAsync(int contentId, string authorId, string text);

    /// <summary>
    /// Delete a comment (soft delete)
    /// </summary>
    Task DeleteCommentAsync(int commentId, string userId, bool isAdmin);
}
