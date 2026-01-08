using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;
using RTUB.Application.Utilities;


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

    public async Task<Album> CreateAlbumAsync(string title, int? year, string? description = null, string? imageUrl = null, bool isPrivate = false)
    {
        var album = Album.Create(title, year, description, isPrivate);
        if (!string.IsNullOrEmpty(imageUrl))
        {
            album.SetCoverImage(imageUrl);
        }
        return await _albumRepository.AddAsync(album);
    }

    public async Task UpdateAlbumAsync(int id, string title, int? year, string? description, bool isPrivate)
    {
        var album = await _albumRepository.GetByIdOrThrowAsync(id);

        album.UpdateDetails(title, year, description, isPrivate);
        await _albumRepository.UpdateAsync(album);
    }

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

    public async Task UpdateAlbumWithCoverAsync(int id, string title, int? year, string? description, bool isPrivate, Stream imageStream, string fileName, string contentType)
    {
        var album = await _albumRepository.GetByIdOrThrowAsync(id);

        // Update album details
        album.UpdateDetails(title, year, description, isPrivate);

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
