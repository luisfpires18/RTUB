using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Application.Utilities;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;


namespace RTUB.Application.Services;

/// <summary>
/// Service for managing photo albums using Repository pattern
/// Contains business logic and image storage operations
/// Now depends on IAlbumRepository abstraction instead of concrete DbContext
/// </summary>
public class AlbumService : IAlbumService
{
    private readonly IAlbumRepository _albumRepository;
    private readonly IImageStorageService _imageStorageService;

    /// <summary>
    /// Initializes a new instance of the AlbumService
    /// </summary>
    /// <param name="albumRepository">Repository for album operations</param>
    /// <param name="imageStorageService">Service for image storage operations</param>
    public AlbumService(IAlbumRepository albumRepository, IImageStorageService imageStorageService)
    {
        _albumRepository = albumRepository;
        _imageStorageService = imageStorageService;
    }

    /// <summary>
    /// Gets an album by its ID
    /// </summary>
    /// <param name="id">The ID of the album to retrieve</param>
    /// <returns>The album if found, null otherwise</returns>
    public async Task<Album?> GetAlbumByIdAsync(int id)
    {
        return await _albumRepository.GetByIdAsync(id);
    }

    /// <summary>
    /// Gets all albums
    /// </summary>
    /// <returns>Collection of all albums</returns>
    public async Task<IEnumerable<Album>> GetAllAlbumsAsync()
    {
        return await _albumRepository.GetAllAsync();
    }

    /// <summary>
    /// Gets all public albums
    /// </summary>
    /// <returns>Collection of public albums</returns>
    public async Task<IEnumerable<Album>> GetPublicAlbumsAsync()
    {
        return await _albumRepository.GetPublicAlbumsAsync();
    }

    /// <summary>
    /// Gets all albums that have associated songs
    /// </summary>
    /// <returns>Collection of albums with songs</returns>
    public async Task<IEnumerable<Album>> GetAlbumsWithSongsAsync()
    {
        return await _albumRepository.GetAlbumsWithSongsAsync();
    }

    /// <summary>
    /// Gets an album by its ID with all associated songs
    /// </summary>
    /// <param name="id">The ID of the album to retrieve</param>
    /// <returns>The album with songs if found, null otherwise</returns>
    public async Task<Album?> GetAlbumWithSongsAsync(int id)
    {
        return await _albumRepository.GetAlbumWithSongsAsync(id);
    }

    /// <summary>
    /// Creates a new album
    /// </summary>
    /// <param name="title">The title of the album</param>
    /// <param name="year">The year of the album (optional)</param>
    /// <param name="description">Optional description</param>
    /// <param name="imageUrl">Optional cover image URL</param>
    /// <param name="isPrivate">Whether the album is private (default: false)</param>
    /// <param name="isExclusive">Whether the album is exclusive (default: false)</param>
    /// <returns>The created album</returns>
    public async Task<Album> CreateAlbumAsync(string title, int? year, string? description = null, string? imageUrl = null, bool isPrivate = false, bool isExclusive = false)
    {
        var album = Album.Create(title, year, description, isPrivate, isExclusive);
        if (!string.IsNullOrEmpty(imageUrl))
        {
            album.SetCoverImage(imageUrl);
        }
        return await _albumRepository.AddAsync(album);
    }

    /// <summary>
    /// Creates a new exclusive album with authorized user access
    /// </summary>
    /// <param name="title">The title of the album</param>
    /// <param name="year">The year of the album (optional)</param>
    /// <param name="description">Optional description</param>
    /// <param name="imageUrl">Optional cover image URL</param>
    /// <param name="authorizedUserIds">List of user IDs authorized to access this exclusive album</param>
    /// <returns>The created exclusive album</returns>
    public async Task<Album> CreateExclusiveAlbumAsync(string title, int? year, string? description, string? imageUrl, List<string> authorizedUserIds)
    {
        // Filter out any empty or null user IDs
        var validUserIds = authorizedUserIds?.Where(id => !string.IsNullOrWhiteSpace(id)).ToList() ?? new List<string>();

        // Create the album as exclusive
        var album = await CreateAlbumAsync(title, year, description, imageUrl, isPrivate: false, isExclusive: true);

        // Add authorized users to the access list (batch without saving each time)
        foreach (var userId in validUserIds)
        {
            await _albumRepository.AddAlbumAccessAsync(album.Id, userId, saveChanges: false);
        }

        // Save all access entries in a single transaction
        if (validUserIds.Count > 0)
        {
            await _albumRepository.SaveChangesAsync();
        }

        return album;
    }

    /// <summary>
    /// Updates an existing album
    /// </summary>
    /// <param name="id">The ID of the album to update</param>
    /// <param name="title">The new title</param>
    /// <param name="year">The new year (optional)</param>
    /// <param name="description">The new description</param>
    /// <param name="isPrivate">Whether the album is private</param>
    /// <param name="isExclusive">Whether the album is exclusive (default: false)</param>
    /// <exception cref="EntityNotFoundException">Thrown when the album is not found</exception>
    public async Task UpdateAlbumAsync(int id, string title, int? year, string? description, bool isPrivate, bool isExclusive = false)
    {
        var album = await _albumRepository.GetByIdOrThrowAsync(id);

        album.UpdateDetails(title, year, description, isPrivate, isExclusive);
        await _albumRepository.UpdateAsync(album);
    }

    /// <summary>
    /// Updates the authorized user access list for an exclusive album
    /// </summary>
    /// <param name="albumId">The ID of the album</param>
    /// <param name="authorizedUserIds">List of user IDs authorized to access this album</param>
    /// <exception cref="EntityNotFoundException">Thrown when the album is not found</exception>
    public async Task UpdateAlbumAccessAsync(int albumId, List<string> authorizedUserIds)
    {
        var album = await _albumRepository.GetByIdOrThrowAsync(albumId);

        // Filter out any empty or null user IDs
        var validUserIds = authorizedUserIds?.Where(id => !string.IsNullOrWhiteSpace(id)).ToHashSet() ?? new HashSet<string>();

        // Get current authorized users
        var currentUserIds = (await _albumRepository.GetAuthorizedUserIdsAsync(albumId)).ToHashSet();

        // Remove users that are no longer authorized (batch without saving each time)
        foreach (var userId in currentUserIds.Where(id => !validUserIds.Contains(id)))
        {
            await _albumRepository.RemoveAlbumAccessAsync(albumId, userId, saveChanges: false);
        }

        // Add new authorized users (batch without saving each time)
        foreach (var userId in validUserIds.Where(id => !currentUserIds.Contains(id)))
        {
            await _albumRepository.AddAlbumAccessAsync(albumId, userId, saveChanges: false);
        }

        // Save all changes in a single transaction
        await _albumRepository.SaveChangesAsync();
    }

    /// <summary>
    /// Gets all authorized user IDs for an exclusive album
    /// </summary>
    /// <param name="albumId">The ID of the album</param>
    /// <returns>Collection of authorized user IDs</returns>
    public async Task<IEnumerable<string>> GetAuthorizedUserIdsAsync(int albumId)
    {
        return await _albumRepository.GetAuthorizedUserIdsAsync(albumId);
    }

    /// <summary>
    /// Gets all albums accessible to a specific user
    /// </summary>
    /// <param name="userId">The ID of the user</param>
    /// <param name="isOwner">Whether the user is an owner/admin (default: false)</param>
    /// <returns>Collection of albums accessible to the user</returns>
    public async Task<IEnumerable<Album>> GetAlbumsForUserAsync(string userId, bool isOwner = false)
    {
        return await _albumRepository.GetAlbumsForUserAsync(userId, isOwner);
    }

    /// <summary>
    /// Checks if a user has access to an album
    /// </summary>
    /// <param name="albumId">The ID of the album</param>
    /// <param name="userId">The ID of the user</param>
    /// <returns>True if the user has access, false otherwise</returns>
    public async Task<bool> HasAccessAsync(int albumId, string userId)
    {
        return await _albumRepository.HasAccessAsync(albumId, userId);
    }

    /// <summary>
    /// Deletes an album and its associated cover image
    /// </summary>
    /// <param name="id">The ID of the album to delete</param>
    /// <exception cref="EntityNotFoundException">Thrown when the album is not found</exception>
    public async Task DeleteAlbumAsync(int id)
    {
        var album = await _albumRepository.GetByIdOrThrowAsync(id);

        // Delete associated image from R2 storage if it exists
        if (!string.IsNullOrEmpty(album.ImageUrl))
        {
            await _imageStorageService.DeleteImageAsync(album.ImageUrl);
        }

        await _albumRepository.DeleteAsync(album);
    }

    /// <summary>
    /// Updates an album and uploads a new cover image
    /// </summary>
    /// <param name="id">The ID of the album to update</param>
    /// <param name="title">The new title</param>
    /// <param name="year">The new year (optional)</param>
    /// <param name="description">The new description</param>
    /// <param name="isPrivate">Whether the album is private</param>
    /// <param name="imageStream">The image stream to upload</param>
    /// <param name="fileName">The name of the image file</param>
    /// <param name="contentType">The content type of the image</param>
    /// <exception cref="EntityNotFoundException">Thrown when the album is not found</exception>
    public async Task UpdateAlbumWithCoverAsync(int id, string title, int? year, string? description, bool isPrivate, Stream imageStream, string fileName, string contentType)
    {
        var album = await _albumRepository.GetByIdOrThrowAsync(id);

        // Update album details (preserve existing IsExclusive value)
        album.UpdateDetails(title, year, description, isPrivate, album.IsExclusive);

        // Delete old image if it exists
        if (!string.IsNullOrEmpty(album.ImageUrl))
        {
            await _imageStorageService.DeleteImageAsync(album.ImageUrl);
        }

        // Upload new image to Cloudflare R2 using normalized album title
        var normalizedName = S3KeyNormalizer.NormalizeForS3Key(title);
        var imageUrl = await _imageStorageService.UploadImageAsync(imageStream, fileName, contentType, "albums", normalizedName);
        album.SetCoverImage(imageUrl);

        await _albumRepository.UpdateAsync(album);
    }

    /// <summary>
    /// Sets or updates the cover image for an album
    /// </summary>
    /// <param name="id">The ID of the album</param>
    /// <param name="imageStream">The image stream to upload</param>
    /// <param name="fileName">The name of the image file</param>
    /// <param name="contentType">The content type of the image</param>
    /// <exception cref="EntityNotFoundException">Thrown when the album is not found</exception>
    public async Task SetAlbumCoverAsync(int id, Stream imageStream, string fileName, string contentType)
    {
        var album = await _albumRepository.GetByIdOrThrowAsync(id);

        // Delete old image if it exists
        if (!string.IsNullOrEmpty(album.ImageUrl))
        {
            await _imageStorageService.DeleteImageAsync(album.ImageUrl);
        }

        // Upload new image to Cloudflare R2 using normalized album title
        var normalizedName = S3KeyNormalizer.NormalizeForS3Key(album.Title);
        var imageUrl = await _imageStorageService.UploadImageAsync(imageStream, fileName, contentType, "albums", normalizedName);
        album.SetCoverImage(imageUrl);

        await _albumRepository.UpdateAsync(album);
    }
}
