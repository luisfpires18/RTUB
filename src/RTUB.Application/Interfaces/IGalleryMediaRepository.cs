using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository for gallery media data access
/// </summary>
public interface IGalleryMediaRepository : IRepository<GalleryMedia>
{
    /// <summary>
    /// Get all gallery media with filters
    /// </summary>
    Task<IEnumerable<GalleryMedia>> GetAllWithDetailsAsync(int? year = null, string? personId = null);

    /// <summary>
    /// Get gallery media by ID with all related data
    /// </summary>
    Task<GalleryMedia?> GetByIdWithDetailsAsync(int id);

    /// <summary>
    /// Get all available years
    /// </summary>
    Task<IEnumerable<int>> GetAvailableYearsAsync();

    /// <summary>
    /// Get paginated gallery media with details
    /// </summary>
    Task<(IEnumerable<GalleryMedia> Items, int TotalCount)> GetPaginatedWithDetailsAsync(
        int page,
        int pageSize,
        int? year = null,
        string? personId = null);
}
