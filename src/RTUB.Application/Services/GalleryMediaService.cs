using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing gallery media (photos and videos)
/// Handles CRUD operations, person tagging, and filtering
/// </summary>
public class GalleryMediaService : IGalleryMediaService
{
    private readonly IGalleryMediaRepository _repository;
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly ApplicationDbContext _context;

    /// <summary>
    /// Initializes a new instance of the GalleryMediaService
    /// </summary>
    /// <param name="repository">Repository for gallery media operations</param>
    /// <param name="context">Database context for direct operations</param>
    public GalleryMediaService(IGalleryMediaRepository repository, IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _repository = repository;
        _contextFactory = contextFactory;
        _context = contextFactory.CreateDbContext();
    }

    /// <summary>
    /// Gets all gallery media with optional filtering
    /// </summary>
    /// <param name="year">Optional year filter</param>
    /// <param name="personId">Optional person ID filter</param>
    /// <param name="isAuthenticated">Optional authentication status filter</param>
    /// <returns>Collection of gallery media matching the filters</returns>
    public async Task<IEnumerable<GalleryMedia>> GetAllAsync(int? year = null, string? personId = null, bool? isAuthenticated = null)
    {
        return await _repository.GetAllWithDetailsAsync(year, personId, isAuthenticated);
    }

    /// <summary>
    /// Gets a gallery media item by its ID with all related details
    /// </summary>
    /// <param name="id">The ID of the media to retrieve</param>
    /// <returns>The gallery media if found, null otherwise</returns>
    public async Task<GalleryMedia?> GetByIdAsync(int id)
    {
        return await _repository.GetByIdWithDetailsAsync(id);
    }

    /// <summary>
    /// Creates a new gallery media item
    /// </summary>
    /// <param name="media">The gallery media entity to create</param>
    /// <returns>The created gallery media</returns>
    public async Task<GalleryMedia> CreateAsync(GalleryMedia media)
    {
        await _repository.AddAsync(media);
        await _context.SaveChangesAsync();
        return media;
    }

    /// <summary>
    /// Updates an existing gallery media item
    /// </summary>
    /// <param name="media">The gallery media entity with updated values</param>
    /// <returns>The updated gallery media</returns>
    /// <exception cref="InvalidOperationException">Thrown when the media is not found</exception>
    public async Task<GalleryMedia> UpdateAsync(GalleryMedia media)
    {
        // Load the existing tracked entity from the context
        var existingMedia = await _context.GalleryMedia
            .FirstOrDefaultAsync(m => m.Id == media.Id);

        if (existingMedia == null)
            throw new InvalidOperationException($"Media with ID {media.Id} not found");

        // Update only the allowed fields on the tracked entity
        existingMedia.UpdateDetails(media.Title, media.Year, media.Month, media.Day, media.TakenAt);
        existingMedia.UpdatePrivacy(media.IsPrivate);

        await _context.SaveChangesAsync();
        return existingMedia;
    }

    /// <summary>
    /// Deletes a gallery media item
    /// </summary>
    /// <param name="id">The ID of the media to delete</param>
    public async Task DeleteAsync(int id)
    {
        await _repository.DeleteAsync(id);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Adds person tags to a gallery media item
    /// </summary>
    /// <param name="mediaId">The ID of the media</param>
    /// <param name="personIds">Collection of person IDs to tag</param>
    /// <exception cref="InvalidOperationException">Thrown when the media is not found</exception>
    public async Task AddPersonTagsAsync(int mediaId, IEnumerable<string> personIds)
    {
        var media = await _context.GalleryMedia
            .Include(m => m.PeopleInMedia)
            .FirstOrDefaultAsync(m => m.Id == mediaId);

        if (media == null)
            throw new InvalidOperationException("Media not found");

        foreach (var personId in personIds)
        {
            if (!media.PeopleInMedia.Any(p => p.UserId == personId))
            {
                media.PeopleInMedia.Add(GalleryMediaPersonTag.Create(mediaId, personId));
            }
        }

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Removes person tags from a gallery media item
    /// </summary>
    /// <param name="mediaId">The ID of the media</param>
    /// <param name="personIds">Collection of person IDs to remove tags for</param>
    /// <exception cref="InvalidOperationException">Thrown when the media is not found</exception>
    public async Task RemovePersonTagsAsync(int mediaId, IEnumerable<string> personIds)
    {
        var media = await _context.GalleryMedia
            .Include(m => m.PeopleInMedia)
            .FirstOrDefaultAsync(m => m.Id == mediaId);

        if (media == null)
            throw new InvalidOperationException("Media not found");

        var tagsToRemove = media.PeopleInMedia
            .Where(p => personIds.Contains(p.UserId))
            .ToList();

        foreach (var tag in tagsToRemove)
        {
            media.PeopleInMedia.Remove(tag);
        }

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Gets all available years for gallery media
    /// </summary>
    /// <param name="isAuthenticated">Optional filter by authentication status</param>
    /// <returns>Collection of available years</returns>
    public async Task<IEnumerable<int>> GetAvailableYearsAsync(bool? isAuthenticated = null)
    {
        return await _repository.GetAvailableYearsAsync(isAuthenticated);
    }

    /// <summary>
    /// Gets paginated gallery media with filtering and search support
    /// </summary>
    /// <param name="page">The page number (1-based)</param>
    /// <param name="pageSize">The number of items per page</param>
    /// <param name="year">Optional year filter</param>
    /// <param name="personId">Optional person ID filter</param>
    /// <param name="isAuthenticated">Optional authentication status filter</param>
    /// <param name="titleSearch">Optional search term for title</param>
    /// <returns>A tuple containing the media items for the page and the total count</returns>
    public async Task<(IEnumerable<GalleryMedia> Items, int TotalCount)> GetPaginatedAsync(
        int page,
        int pageSize,
        int? year = null,
        string? personId = null,
        bool? isAuthenticated = null,
        string? titleSearch = null)
    {
        return await _repository.GetPaginatedWithDetailsAsync(page, pageSize, year, personId, isAuthenticated, titleSearch);
    }
}
