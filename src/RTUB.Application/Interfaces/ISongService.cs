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
    Task<IEnumerable<SongVideo>> GetVideosBySongIdAsync(int songId);
    Task<SongVideo> AddVideoAsync(int songId, Stream fileStream, string fileName, string contentType, string createdByUserId, string? title = null);
    Task DeleteVideoAsync(int videoId, string userId, bool isAdmin = false);
    Task<int> GetVideoCountBySongIdAsync(int songId);
    
    // Play count tracking
    Task IncrementPlayCountAsync(int songId, string? userId = null);
    Task<int> GetPlayCountBySongIdAsync(int songId);
    Task<Dictionary<int, int>> GetPlayCountsForSongsAsync(IEnumerable<int> songIds);
    Task<IEnumerable<(Song Song, int PlayCount)>> GetTopSongsAsync(int count = 10);
    Task<IEnumerable<(Album Album, int PlayCount)>> GetTopAlbumsAsync(int count = 10);
    Task<IEnumerable<(UserPlayInfo User, Song Song, Album Album, int PlayCount)>> GetDetailedPlayStatsAsync();
    Task<IEnumerable<(Song Song, int PlayCount)>> GetAllSongsWithPlayCountAsync();
    Task<IEnumerable<(Album Album, int PlayCount)>> GetAllAlbumsWithPlayCountAsync();
}

/// <summary>
/// User information for play statistics
/// </summary>
public class UserPlayInfo
{
    public string UserId { get; set; } = "";
    public string Nickname { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string ProfilePictureSrc { get; set; } = "";
}
