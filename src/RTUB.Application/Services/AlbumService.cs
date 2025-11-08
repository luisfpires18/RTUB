using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using Microsoft.Extensions.Logging;

namespace RTUB.Application.Services;

public class AlbumService : IAlbumService
{
    private readonly ApplicationDbContext _context;
    private readonly IImageService _imageService;
    private readonly IAlbumStorageService _albumStorageService;
    private readonly ILogger<AlbumService> _logger;

    public AlbumService(
        ApplicationDbContext context, 
        IImageService imageService,
        IAlbumStorageService albumStorageService,
        ILogger<AlbumService> logger)
    {
        _context = context;
        _imageService = imageService;
        _albumStorageService = albumStorageService;
        _logger = logger;
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

    public async Task<Album> CreateAlbumAsync(string title, int? year, string? description = null)
    {
        var album = Album.Create(title, year, description);
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

        // Delete S3 image if exists
        if (!string.IsNullOrEmpty(album.ImageUrl))
        {
            // Extract filename from URL and delete from S3
            var filename = ExtractFilenameFromUrl(album.ImageUrl);
            if (!string.IsNullOrEmpty(filename))
            {
                await _albumStorageService.DeleteImageAsync(filename);
            }
        }

        _context.Albums.Remove(album);
        await _context.SaveChangesAsync();
    }

    public async Task SetAlbumCoverAsync(int id, byte[]? imageData, string? contentType, string url = "")
    {
        var album = await _context.Albums.FindAsync(id);
        if (album == null)
            throw new InvalidOperationException($"Album with ID {id} not found");

        // If imageData is provided, upload to IDrive S3
        if (imageData != null && !string.IsNullOrEmpty(contentType))
        {
            // STRATEGY: Replace-Before-Upload
            // Step 1: Delete old image BEFORE uploading new one
            if (!string.IsNullOrEmpty(album.ImageUrl))
            {
                // Extract filename and delete from S3
                var oldFilename = ExtractFilenameFromUrl(album.ImageUrl);
                if (!string.IsNullOrEmpty(oldFilename))
                {
                    _logger.LogInformation("Deleting old album cover from S3: {Filename}", oldFilename);
                    await _albumStorageService.DeleteImageAsync(oldFilename);
                }
            }
            
            // Step 2: Upload new image to IDrive S3 and get the filename
            var filename = await _albumStorageService.UploadImageAsync(imageData, id, contentType);
            
            // Step 3: Get the public URL from the storage service
            var imageUrl = await _albumStorageService.GetImageUrlAsync(filename);
            
            // Step 4: Store the full URL in the ImageUrl field
            album.SetImageUrl(imageUrl);
        }
        else if (!string.IsNullOrEmpty(url))
        {
            // If no imageData provided, just update the URL
            album.SetImageUrl(url);
        }
        
        _context.Albums.Update(album);
        await _context.SaveChangesAsync();
        
        // Invalidate the cached album image so the new image is served immediately
        _imageService.InvalidateAlbumImageCache(id);
    }

    private string? ExtractFilenameFromUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return null;

        try
        {
            // Extract filename from URL path (last segment after the last '/')
            var uri = new Uri(url);
            var segments = uri.AbsolutePath.Split('/');
            return segments.Length > 0 ? segments[^1] : null;
        }
        catch
        {
            return null;
        }
    }
}
