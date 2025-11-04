using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Song service implementation
/// Contains business logic for song operations
/// Follows Single Responsibility and Dependency Inversion principles
/// </summary>
public class SongService : ISongService
{
    private readonly ApplicationDbContext _context;

    public SongService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Song?> GetSongByIdAsync(int id)
    {
        return await _context.Songs.FindAsync(id);
    }

    public async Task<IEnumerable<Song>> GetAllSongsAsync()
    {
        return await _context.Songs
            .Include(s => s.Album)
            .ToListAsync();
    }

    public async Task<IEnumerable<Song>> GetSongsByAlbumIdAsync(int albumId)
    {
        return await _context.Songs
            .Where(s => s.AlbumId == albumId)
            .OrderBy(s => s.TrackNumber)
            .ToListAsync();
    }

    public async Task<Song> CreateSongAsync(string title, int albumId, int? trackNumber = null)
    {
        var song = Song.Create(title, albumId, trackNumber);
        _context.Songs.Add(song);
        await _context.SaveChangesAsync();
        return song;
    }

    public async Task UpdateSongAsync(int id, string title, int? trackNumber, string? lyricAuthor, string? musicAuthor, string? adaptation, int? duration)
    {
        var song = await _context.Songs.FindAsync(id);
        if (song == null)
            throw new InvalidOperationException($"Song with ID {id} not found");

        song.UpdateDetails(title, trackNumber, lyricAuthor, musicAuthor, adaptation, duration);
        _context.Songs.Update(song);
        await _context.SaveChangesAsync();
    }

    public async Task SetSongLyricsAsync(int id, string? lyrics)
    {
        var song = await _context.Songs.FindAsync(id);
        if (song == null)
            throw new InvalidOperationException($"Song with ID {id} not found");

        song.SetLyrics(lyrics);
        _context.Songs.Update(song);
        await _context.SaveChangesAsync();
    }

    public async Task SetSongSpotifyUrlAsync(int id, string? url)
    {
        var song = await _context.Songs.FindAsync(id);
        if (song == null)
            throw new InvalidOperationException($"Song with ID {id} not found");

        song.SetSpotifyUrl(url);
        _context.Songs.Update(song);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteSongAsync(int id)
    {
        var song = await _context.Songs.FindAsync(id);
        if (song == null)
            throw new InvalidOperationException($"Song with ID {id} not found");

        _context.Songs.Remove(song);
        await _context.SaveChangesAsync();
    }

    public async Task AddYouTubeUrlAsync(int songId, string url)
    {
        var song = await _context.Songs.FindAsync(songId);
        if (song == null)
            throw new InvalidOperationException($"Song with ID {songId} not found");

        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("YouTube URL cannot be empty", nameof(url));

        // Add YouTube URL to song
        var youtubeUrl = new SongYouTubeUrl 
        { 
            SongId = songId, 
            Url = url.Trim() 
        };
        
        song.YouTubeUrls.Add(youtubeUrl);
        _context.Songs.Update(song);
        await _context.SaveChangesAsync();
    }

    public async Task RemoveYouTubeUrlAsync(int songId, string url)
    {
        var song = await _context.Songs.FindAsync(songId);
        if (song == null)
            throw new InvalidOperationException($"Song with ID {songId} not found");

        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("YouTube URL cannot be empty", nameof(url));

        // Remove YouTube URL from song
        var youtubeUrl = song.YouTubeUrls.FirstOrDefault(y => y.Url == url.Trim());
        if (youtubeUrl != null)
        {
            song.YouTubeUrls.Remove(youtubeUrl);
            _context.Songs.Update(song);
            await _context.SaveChangesAsync();
        }
    }
}
