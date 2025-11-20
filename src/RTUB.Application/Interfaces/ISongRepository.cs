using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for Song entity with domain-specific operations
/// Extends generic repository with song-specific queries
/// </summary>
public interface ISongRepository : IRepository<Song>
{
    /// <summary>
    /// Gets a song by ID with YouTubeUrls included
    /// </summary>
    /// <param name="id">Song ID</param>
    /// <returns>Song with YouTubeUrls, or null if not found</returns>
    Task<Song?> GetSongByIdWithUrlsAsync(int id);

    /// <summary>
    /// Gets all songs with album information included
    /// </summary>
    /// <returns>Collection of songs with album data</returns>
    Task<IEnumerable<Song>> GetAllSongsWithAlbumAsync();

    /// <summary>
    /// Gets songs by album ID ordered by track number
    /// </summary>
    /// <param name="albumId">Album ID filter</param>
    /// <returns>Collection of songs in the album</returns>
    Task<IEnumerable<Song>> GetSongsByAlbumIdAsync(int albumId);

    /// <summary>
    /// Gets a song with tracking enabled for updates (includes YouTubeUrls)
    /// </summary>
    /// <param name="id">Song ID</param>
    /// <returns>Tracked song entity, or null if not found</returns>
    Task<Song?> GetSongForUpdateAsync(int id);
}
