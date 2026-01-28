using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for processing album and song statistics
/// Extracted from Albums.razor to improve separation of concerns
/// </summary>
public interface IAlbumStatisticsService
{
    /// <summary>
    /// Loads all statistics data including songs, albums, and user stats (if admin)
    /// </summary>
    /// <param name="isAdmin">Whether the current user is an admin (determines if detailed stats are loaded)</param>
    /// <returns>Statistics data including songs, albums, and optionally user stats</returns>
    Task<AlbumStatisticsDto> LoadStatisticsAsync(bool isAdmin);
}

/// <summary>
/// DTO for album statistics data
/// </summary>
public class AlbumStatisticsDto
{
    public List<(Song Song, int PlayCount)> SongsStats { get; set; } = new();
    public List<(Album Album, int PlayCount)> AlbumsStats { get; set; } = new();
    public List<UserStatsGroupDto>? UserStats { get; set; }
}

/// <summary>
/// DTO for user statistics group
/// </summary>
public class UserStatsGroupDto
{
    public string UserId { get; set; } = "";
    public string Nickname { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string ProfilePictureSrc { get; set; } = "";
    public List<SongPlayInfoDto> Songs { get; set; } = new();
    public int TotalPlays => Songs.Sum(s => s.PlayCount);
}

/// <summary>
/// DTO for song play information
/// </summary>
public class SongPlayInfoDto
{
    public string SongTitle { get; set; } = "";
    public string AlbumTitle { get; set; } = "";
    public int PlayCount { get; set; }
}
