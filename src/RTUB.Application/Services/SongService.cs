using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Services;

/// <summary>
/// Song service implementation
/// Contains business logic for song operations
/// Follows Single Responsibility and Dependency Inversion principles
/// </summary>
public class SongService : ISongService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SongService>? _logger;

    public SongService(ApplicationDbContext context, ILogger<SongService>? logger = null)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Gets the base query for songs with common includes for YouTubeUrls
    /// </summary>
    private IQueryable<Song> GetBaseSongQuery(bool includeAlbum = false)
    {
        IQueryable<Song> query = _context.Songs.AsNoTracking();
        
        if (includeAlbum)
        {
            query = query.Include(s => s.Album);
        }
        
        query = query.Include(s => s.YouTubeUrls);
        
        return query;
    }

    /// <summary>
    /// Gets the base query for songs with tracking enabled (for updates)
    /// </summary>
    private IQueryable<Song> GetBaseSongQueryWithTracking()
    {
        return _context.Songs
            .Include(s => s.YouTubeUrls);
    }

    public async Task<Song?> GetSongByIdAsync(int id)
    {
        return await GetBaseSongQuery()
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<IEnumerable<Song>> GetAllSongsAsync()
    {
        return await GetBaseSongQuery(includeAlbum: true)
            .ToListAsync();
    }

    public async Task<IEnumerable<Song>> GetSongsByAlbumIdAsync(int albumId)
    {
        return await GetBaseSongQuery()
            .Where(s => s.AlbumId == albumId)
            .OrderBy(s => s.TrackNumber)
            .ToListAsync();
    }

    public async Task<Song> CreateSongAsync(string title, int albumId, int? trackNumber = null, string? lyricAuthor = null, string? musicAuthor = null, string? adaptation = null, int? duration = null, string? spotifyUrl = null, bool hasMusic = false)
    {
        var song = Song.Create(title, albumId, trackNumber, lyricAuthor, musicAuthor, adaptation, duration, spotifyUrl, hasMusic);
        _context.Songs.Add(song);
        await _context.SaveChangesAsync();
        return song;
    }

    public async Task UpdateSongAsync(int id, string title, int? trackNumber, string? lyricAuthor, string? musicAuthor, string? adaptation, int? duration)
    {
        var song = await GetBaseSongQueryWithTracking()
            .FirstOrDefaultAsync(s => s.Id == id);
            
        if (song == null)
            throw new EntityNotFoundException(nameof(Song), id);

        song.UpdateDetails(title, trackNumber, lyricAuthor, musicAuthor, adaptation, duration);
        // EF Core change tracker automatically detects modifications to loaded entities
        await _context.SaveChangesAsync();
    }

    public async Task SetSongLyricsAsync(int id, string? lyrics)
    {
        var song = await GetBaseSongQueryWithTracking()
            .FirstOrDefaultAsync(s => s.Id == id);
            
        if (song == null)
            throw new EntityNotFoundException(nameof(Song), id);

        song.SetLyrics(lyrics);
        // EF Core change tracker automatically detects modifications to loaded entities
        await _context.SaveChangesAsync();
    }

    public async Task SetSongSpotifyUrlAsync(int id, string? url)
    {
        var song = await GetBaseSongQueryWithTracking()
            .FirstOrDefaultAsync(s => s.Id == id);
            
        if (song == null)
            throw new EntityNotFoundException(nameof(Song), id);

        song.SetSpotifyUrl(url);
        // EF Core change tracker automatically detects modifications to loaded entities
        await _context.SaveChangesAsync();
    }

    public async Task SetSongHasMusicAsync(int id, bool hasMusic)
    {
        var song = await GetBaseSongQueryWithTracking()
            .FirstOrDefaultAsync(s => s.Id == id);
            
        if (song == null)
            throw new EntityNotFoundException(nameof(Song), id);

        song.SetHasMusic(hasMusic);
        // EF Core change tracker automatically detects modifications to loaded entities
        await _context.SaveChangesAsync();
    }

    public async Task DeleteSongAsync(int id)
    {
        var song = await GetBaseSongQueryWithTracking()
            .FirstOrDefaultAsync(s => s.Id == id);
            
        if (song == null)
            throw new EntityNotFoundException(nameof(Song), id);

        _context.Songs.Remove(song);
        await _context.SaveChangesAsync();
    }

    public async Task AddYouTubeUrlAsync(int songId, string url)
    {
        var song = await GetBaseSongQueryWithTracking()
            .FirstOrDefaultAsync(s => s.Id == songId);
            
        if (song == null)
            throw new EntityNotFoundException(nameof(Song), songId);

        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("YouTube URL cannot be empty", nameof(url));

        var canonicalUrl = CanonicalizeUrl(url.Trim());

        // Check if URL already exists (avoid duplicates)
        if (song.YouTubeUrls.Any(y => CanonicalizeUrl(y.Url) == canonicalUrl))
        {
            return; // URL already exists, don't add duplicate
        }

        // Add YouTube URL to song
        var youtubeUrl = new SongYouTubeUrl 
        { 
            SongId = songId, 
            Url = canonicalUrl 
        };
        
        song.YouTubeUrls.Add(youtubeUrl);
        await _context.SaveChangesAsync();
    }

    public async Task RemoveYouTubeUrlAsync(int songId, string url)
    {
        var song = await GetBaseSongQueryWithTracking()
            .FirstOrDefaultAsync(s => s.Id == songId);
            
        if (song == null)
            throw new EntityNotFoundException(nameof(Song), songId);

        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("YouTube URL cannot be empty", nameof(url));

        var canonicalUrl = CanonicalizeUrl(url.Trim());

        // Remove YouTube URL from song - use canonicalized comparison
        var youtubeUrl = song.YouTubeUrls.FirstOrDefault(y => CanonicalizeUrl(y.Url) == canonicalUrl);
        if (youtubeUrl != null)
        {
            song.YouTubeUrls.Remove(youtubeUrl);
            await _context.SaveChangesAsync();
        }
        else
        {
            _logger?.LogWarning("Attempted to remove non-existent YouTube URL from song {SongId}: {Url}", 
                songId, canonicalUrl);
        }
    }

    /// <summary>
    /// Canonicalizes a URL by trimming and normalizing to lowercase for comparison.
    /// This provides basic normalization for duplicate detection.
    /// Note: Does not handle protocol differences (http vs https), trailing slashes,
    /// or query parameter ordering - these are preserved from user input.
    /// </summary>
    private string CanonicalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;
            
        return url.Trim().ToLowerInvariant();
    }
}
