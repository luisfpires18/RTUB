using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for Song operations
/// Abstracts business logic from presentation layer
/// </summary>
public interface ISongService
{
    Task<Song?> GetSongByIdAsync(int id);
    Task<IEnumerable<Song>> GetAllSongsAsync();
    Task<IEnumerable<Song>> GetSongsByAlbumIdAsync(int albumId);
    Task<Song> CreateSongAsync(string title, int albumId, int? trackNumber = null, string? lyricAuthor = null, string? musicAuthor = null, string? adaptation = null, int? duration = null, string? spotifyUrl = null, bool hasMusic = false);
    Task UpdateSongAsync(int id, string title, int? trackNumber, string? lyricAuthor, string? musicAuthor, string? adaptation, int? duration);
    Task SetSongLyricsAsync(int id, string? lyrics);
    Task SetSongSpotifyUrlAsync(int id, string? url);
    Task SetSongHasMusicAsync(int id, bool hasMusic);
    Task AddYouTubeUrlAsync(int songId, string url);
    Task RemoveYouTubeUrlAsync(int songId, string url);
    Task DeleteSongAsync(int id);
}
