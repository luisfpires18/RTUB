using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;


namespace RTUB.Application.Services;

public class AlbumService : IAlbumService
{
    private readonly ApplicationDbContext _context;
    private readonly IImageService _imageService;

    public AlbumService(ApplicationDbContext context, IImageService imageService)
    {
        _context = context;
        _imageService = imageService;
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

        _context.Albums.Remove(album);
        await _context.SaveChangesAsync();
    }

    public async Task SetAlbumCoverAsync(int id, byte[]? imageData, string? contentType, string url = "")
    {
        var album = await _context.Albums.FindAsync(id);
        if (album == null)
            throw new InvalidOperationException($"Album with ID {id} not found");

        album.SetCoverImage(imageData, contentType, url);
        _context.Albums.Update(album);
        await _context.SaveChangesAsync();
        
        // Invalidate the cached album image so the new image is served immediately
        _imageService.InvalidateAlbumImageCache(id);
    }
}
