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

    public AlbumService(IAlbumRepository albumRepository, IImageStorageService imageStorageService)
    {
        _albumRepository = albumRepository;
        _imageStorageService = imageStorageService;
    }

    public async Task<Album?> GetAlbumByIdAsync(int id)
    {
        return await _albumRepository.GetByIdAsync(id);
    }

    public async Task<IEnumerable<Album>> GetAllAlbumsAsync()
    {
        return await _albumRepository.GetAllAsync();
    }

    public async Task<IEnumerable<Album>> GetPublicAlbumsAsync()
    {
        return await _albumRepository.GetPublicAlbumsAsync();
    }

    public async Task<IEnumerable<Album>> GetAlbumsWithSongsAsync()
    {
        return await _albumRepository.GetAlbumsWithSongsAsync();
    }

    public async Task<Album?> GetAlbumWithSongsAsync(int id)
    {
        return await _albumRepository.GetAlbumWithSongsAsync(id);
    }

    public async Task<Album> CreateAlbumAsync(string title, int? year, string? description = null, string? imageUrl = null, bool isPrivate = false, bool isExclusive = false)
    {
        var album = Album.Create(title, year, description, isPrivate, isExclusive);
        if (!string.IsNullOrEmpty(imageUrl))
        {
            album.SetCoverImage(imageUrl);
        }
        return await _albumRepository.AddAsync(album);
    }

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

    public async Task UpdateAlbumAsync(int id, string title, int? year, string? description, bool isPrivate, bool isExclusive = false)
    {
        var album = await _albumRepository.GetByIdAsync(id);
        if (album == null)
            throw new EntityNotFoundException(nameof(Album), id);

        album.UpdateDetails(title, year, description, isPrivate, isExclusive);
        await _albumRepository.UpdateAsync(album);
    }

    public async Task UpdateAlbumAccessAsync(int albumId, List<string> authorizedUserIds)
    {
        var album = await _albumRepository.GetByIdAsync(albumId);
        if (album == null)
            throw new EntityNotFoundException(nameof(Album), albumId);

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

    public async Task<IEnumerable<string>> GetAuthorizedUserIdsAsync(int albumId)
    {
        return await _albumRepository.GetAuthorizedUserIdsAsync(albumId);
    }

    public async Task<IEnumerable<Album>> GetAlbumsForUserAsync(string userId, bool isOwner = false)
    {
        return await _albumRepository.GetAlbumsForUserAsync(userId, isOwner);
    }

    public async Task<bool> HasAccessAsync(int albumId, string userId)
    {
        return await _albumRepository.HasAccessAsync(albumId, userId);
    }

    public async Task DeleteAlbumAsync(int id)
    {
        var album = await _albumRepository.GetByIdAsync(id);
        if (album == null)
            throw new EntityNotFoundException(nameof(Album), id);

        // Delete associated image from R2 storage if it exists
        if (!string.IsNullOrEmpty(album.ImageUrl))
        {
            await _imageStorageService.DeleteImageAsync(album.ImageUrl);
        }

        await _albumRepository.DeleteAsync(album);
    }

    public async Task UpdateAlbumWithCoverAsync(int id, string title, int? year, string? description, bool isPrivate, Stream imageStream, string fileName, string contentType)
    {
        var album = await _albumRepository.GetByIdAsync(id);
        if (album == null)
            throw new EntityNotFoundException(nameof(Album), id);

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

    public async Task SetAlbumCoverAsync(int id, Stream imageStream, string fileName, string contentType)
    {
        var album = await _albumRepository.GetByIdAsync(id);
        if (album == null)
            throw new EntityNotFoundException(nameof(Album), id);

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
