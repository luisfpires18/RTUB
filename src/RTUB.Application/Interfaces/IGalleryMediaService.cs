using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for managing gallery media (images and videos)
/// </summary>
public interface IGalleryMediaService
{
    /// <summary>
    /// Get all gallery media items with optional filters
    /// </summary>
    Task<IEnumerable<GalleryMedia>> GetAllAsync(int? year = null, string? personId = null, bool? isAuthenticated = null);

    /// <summary>
    /// Get gallery media by ID
    /// </summary>
    Task<GalleryMedia?> GetByIdAsync(int id);

    /// <summary>
    /// Create a new gallery media item
    /// </summary>
    Task<GalleryMedia> CreateAsync(GalleryMedia media);

    /// <summary>
    /// Update an existing gallery media item
    /// </summary>
    Task<GalleryMedia> UpdateAsync(GalleryMedia media);

    /// <summary>
    /// Delete a gallery media item
    /// </summary>
    Task DeleteAsync(int id);

    /// <summary>
    /// Add person tags to a media item
    /// </summary>
    Task AddPersonTagsAsync(int mediaId, IEnumerable<string> personIds);

    /// <summary>
    /// Remove person tags from a media item
    /// </summary>
    Task RemovePersonTagsAsync(int mediaId, IEnumerable<string> personIds);

    /// <summary>
    /// Get all years that have gallery media
    /// </summary>
    Task<IEnumerable<int>> GetAvailableYearsAsync(bool? isAuthenticated = null);

    /// <summary>
    /// Get paginated gallery media
    /// </summary>
    Task<(IEnumerable<GalleryMedia> Items, int TotalCount)> GetPaginatedAsync(
        int page,
        int pageSize,
        int? year = null,
        string? personId = null,
        bool? isAuthenticated = null);
}
