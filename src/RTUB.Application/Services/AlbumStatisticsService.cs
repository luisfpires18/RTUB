using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Service for processing album and song statistics
/// Extracted from Albums.razor to improve separation of concerns
/// </summary>
public class AlbumStatisticsService : IAlbumStatisticsService
{
    private readonly ISongService _songService;

    public AlbumStatisticsService(ISongService songService)
    {
        _songService = songService;
    }

    public async Task<AlbumStatisticsDto> LoadStatisticsAsync(bool isAdmin)
    {
        // Load all statistics data (not just top N)
        var allSongsTask = _songService.GetAllSongsWithPlayCountAsync();
        var allAlbumsTask = _songService.GetAllAlbumsWithPlayCountAsync();
        
        Task<IEnumerable<(UserPlayInfo User, Song Song, Album Album, int PlayCount)>>? detailedStatsTask = null;
        if (isAdmin)
        {
            detailedStatsTask = _songService.GetDetailedPlayStatsAsync();
        }
        
        // Wait for tasks to complete
        var songsStats = (await allSongsTask).ToList();
        var albumsStats = (await allAlbumsTask).ToList();
        
        List<UserStatsGroupDto>? userStats = null;
        if (detailedStatsTask != null)
        {
            var detailedStats = (await detailedStatsTask).ToList();
            
            // Group by user
            userStats = detailedStats
                .GroupBy(s => s.User.UserId)
                .Select(g => new UserStatsGroupDto
                {
                    UserId = g.First().User.UserId,
                    Nickname = g.First().User.Nickname,
                    FirstName = g.First().User.FirstName,
                    LastName = g.First().User.LastName,
                    ProfilePictureSrc = g.First().User.ProfilePictureSrc,
                    Songs = g.Select(s => new SongPlayInfoDto
                    {
                        SongTitle = s.Song.Title,
                        AlbumTitle = s.Album.Title,
                        PlayCount = s.PlayCount
                    })
                    .OrderByDescending(s => s.PlayCount)
                    .ToList()
                })
                .OrderByDescending(u => u.TotalPlays)
                .ToList();
        }
        
        return new AlbumStatisticsDto
        {
            SongsStats = songsStats,
            AlbumsStats = albumsStats,
            UserStats = userStats
        };
    }
}
