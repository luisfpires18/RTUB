using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Utilities;


namespace RTUB.Application.Services;

/// <summary>
/// Service for managing photo albums and their operations.
/// </summary>
public class AlbumService : IAlbumService
{
    private readonly ApplicationDbContext _context;
    private readonly IImageStorageService _imageStorageService;

    public AlbumService(ApplicationDbContext context, IImageStorageService imageStorageService)
    {
        _context = context;
        _imageStorageService = imageStorageService;
    }

    public async Task<Album?> GetAlbumByIdAsync(int id)
    {
        return await _context.Albums.FindAsync(id);
    }

    public async Task<IEnumerable<Album>> GetAllAlbumsAsync()
    {
        return await _context.Albums.ToListAsync();
    }

    public async Task<IEnumerable<Album>> GetAlbumsWithSongsAsync()
    {
        return await _context.Albums
            .Include(a => a.Songs)
            .ToListAsync();
    }

    public async Task<Album?> GetAlbumWithSongsAsync(int id)
    {
        return await _context.Albums
            .Include(a => a.Songs)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<Album> CreateAlbumAsync(string title, int? year, string? description = null, string? imageUrl = null)
    {
        var album = Album.Create(title, year, description);
        if (!string.IsNullOrEmpty(imageUrl))
        {
            album.SetCoverImage(imageUrl);
        }
        _context.Albums.Add(album);
        await _context.SaveChangesAsync();
        return album;
    }

    public async Task UpdateAlbumAsync(int id, string title, int? year, string? description)
    {
        var album = await _context.Albums.FindAsync(id);
        if (album == null)
            throw new InvalidOperationException($"Album with ID {id} not found");

        album.UpdateDetails(title, year, description);
        _context.Albums.Update(album);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAlbumAsync(int id)
    {
        var album = await _context.Albums.FindAsync(id);
        if (album == null)
            throw new InvalidOperationException($"Album with ID {id} not found");

        // Delete associated image from R2 storage if it exists
        if (!string.IsNullOrEmpty(album.ImageUrl))
        {
            await _imageStorageService.DeleteImageAsync(album.ImageUrl);
        }

        _context.Albums.Remove(album);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAlbumWithCoverAsync(int id, string title, int? year, string? description, Stream imageStream, string fileName, string contentType)
    {
        var album = await _context.Albums.FindAsync(id);
        if (album == null)
            throw new InvalidOperationException($"Album with ID {id} not found");

        // Update album details
        album.UpdateDetails(title, year, description);

        // Delete old image if it exists
        if (!string.IsNullOrEmpty(album.ImageUrl))
        {
            await _imageStorageService.DeleteImageAsync(album.ImageUrl);
        }

        // Upload new image to Cloudflare R2 using normalized album title
        var normalizedName = S3KeyNormalizer.NormalizeForS3Key(title);
        var imageUrl = await _imageStorageService.UploadImageAsync(imageStream, fileName, contentType, "albums", normalizedName);
        album.SetCoverImage(imageUrl);
        
        _context.Albums.Update(album);
        await _context.SaveChangesAsync();
    }

    public async Task SetAlbumCoverAsync(int id, Stream imageStream, string fileName, string contentType)
    {
        var album = await _context.Albums.FindAsync(id);
        if (album == null)
            throw new InvalidOperationException($"Album with ID {id} not found");

        // Delete old image if it exists
        if (!string.IsNullOrEmpty(album.ImageUrl))
        {
            await _imageStorageService.DeleteImageAsync(album.ImageUrl);
        }

        // Upload new image to Cloudflare R2 using normalized album title
        var normalizedName = S3KeyNormalizer.NormalizeForS3Key(album.Title);
        var imageUrl = await _imageStorageService.UploadImageAsync(imageStream, fileName, contentType, "albums", normalizedName);
        album.SetCoverImage(imageUrl);
        
        _context.Albums.Update(album);
        await _context.SaveChangesAsync();
    }
}
