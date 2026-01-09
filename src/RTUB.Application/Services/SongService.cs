using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Utilities;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Services;

/// <summary>
/// Song service implementation using Repository pattern
/// Contains business logic for song operations
/// Follows Single Responsibility and Dependency Inversion principles
/// Now depends on ISongRepository abstraction instead of concrete DbContext
/// </summary>
public class SongService : ISongService
{
    private readonly ISongRepository _songRepository;
    private readonly ISongVideoRepository _songVideoRepository;
    private readonly ISongVideoStorageService _songVideoStorageService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SongService>? _logger;
    private readonly IPushNotificationFactory _pushNotificationFactory;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SongService(
        ISongRepository songRepository,
        ISongVideoRepository songVideoRepository,
        ISongVideoStorageService songVideoStorageService,
        ApplicationDbContext context,
        IPushNotificationFactory pushNotificationFactory,
        IPushNotificationService pushNotificationService,
        UserManager<ApplicationUser> userManager,
        IHttpContextAccessor httpContextAccessor,
        ILogger<SongService>? logger = null)
    {
        _songRepository = songRepository;
        _songVideoRepository = songVideoRepository;
        _songVideoStorageService = songVideoStorageService;
        _context = context;
        _logger = logger;
        _pushNotificationFactory = pushNotificationFactory;
        _pushNotificationService = pushNotificationService;
        _userManager = userManager;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Song?> GetSongByIdAsync(int id)
    {
        return await _songRepository.GetSongByIdWithUrlsAsync(id);
    }

    public async Task<IEnumerable<Song>> GetAllSongsAsync()
    {
        return await _songRepository.GetAllSongsWithAlbumAsync();
    }

    public async Task<IEnumerable<Song>> GetSongsByAlbumIdAsync(int albumId)
    {
        return await _songRepository.GetSongsByAlbumIdAsync(albumId);
    }

    public async Task<Song> CreateSongAsync(string title, int albumId, int? trackNumber = null, string? lyricAuthor = null, string? musicAuthor = null, string? adaptation = null, int? duration = null, string? spotifyUrl = null, bool hasMusic = false)
    {
        var song = Song.Create(title, albumId, trackNumber, lyricAuthor, musicAuthor, adaptation, duration, spotifyUrl, hasMusic);
        return await _songRepository.AddAsync(song);
    }

    public async Task UpdateSongAsync(int id, string title, int? trackNumber, string? lyricAuthor, string? musicAuthor, string? adaptation, int? duration)
    {
        var song = await _songRepository.GetSongForUpdateAsync(id);

        if (song == null)
            throw new EntityNotFoundException(nameof(Song), id);

        song.UpdateDetails(title, trackNumber, lyricAuthor, musicAuthor, adaptation, duration);
        await _songRepository.UpdateAsync(song);
    }

    public async Task SetSongLyricsAsync(int id, string? lyrics)
    {
        var song = await _songRepository.GetSongForUpdateAsync(id);

        if (song == null)
            throw new EntityNotFoundException(nameof(Song), id);

        song.SetLyrics(lyrics);
        await _songRepository.UpdateAsync(song);
    }

    public async Task SetSongSpotifyUrlAsync(int id, string? url)
    {
        var song = await _songRepository.GetSongForUpdateAsync(id);

        if (song == null)
            throw new EntityNotFoundException(nameof(Song), id);

        song.SetSpotifyUrl(url);
        await _songRepository.UpdateAsync(song);
    }

    public async Task SetSongHasMusicAsync(int id, bool hasMusic)
    {
        var song = await _songRepository.GetSongForUpdateAsync(id);

        if (song == null)
            throw new EntityNotFoundException(nameof(Song), id);

        song.SetHasMusic(hasMusic);
        await _songRepository.UpdateAsync(song);
    }

    public async Task DeleteSongAsync(int id)
    {
        var song = await _songRepository.GetSongForUpdateAsync(id);

        if (song == null)
            throw new EntityNotFoundException(nameof(Song), id);

        await _songRepository.DeleteAsync(song);
    }

    public async Task AddYouTubeUrlAsync(int songId, string url)
    {
        var song = await _songRepository.GetSongForUpdateAsync(songId);

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
            Url = url.Trim()
        };

        song.YouTubeUrls.Add(youtubeUrl);
        await _songRepository.UpdateAsync(song);
    }

    public async Task RemoveYouTubeUrlAsync(int songId, string url)
    {
        var song = await _songRepository.GetSongForUpdateAsync(songId);

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
            await _songRepository.UpdateAsync(song);
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

    public async Task<IEnumerable<SongVideo>> GetVideosBySongIdAsync(int songId)
    {
        return await _songVideoRepository.GetBySongIdAsync(songId);
    }

    public async Task<SongVideo> AddVideoAsync(int songId, Stream fileStream, string fileName, string contentType, string createdByUserId, string? title = null)
    {
        // Validate that song exists
        var song = await _songRepository.GetSongForUpdateAsync(songId);
        if (song == null)
            throw new EntityNotFoundException(nameof(Song), songId);

        // Ensure we have a valid MIME type (mobile uploads may have empty/incorrect contentType)
        var mimeType = MimeTypeHelper.GetVideoMimeType(fileName, contentType);

        // Upload video to storage
        var videoUrl = await _songVideoStorageService.UploadVideoAsync(fileStream, fileName, mimeType, songId);

        // Get file size from stream position (if seekable)
        long sizeBytes = 0;
        if (fileStream.CanSeek)
        {
            sizeBytes = fileStream.Length;
        }

        // Determine sort order (next available position)
        var existingVideosCount = await _songVideoRepository.GetCountBySongIdAsync(songId);
        var sortOrder = existingVideosCount;

        // Create SongVideo entity
        var songVideo = SongVideo.CreateVideo(
            songId,
            videoUrl,
            mimeType,
            sizeBytes,
            createdByUserId,
            title,
            sortOrder
        );

        // Save to repository
        var createdVideo = await _songVideoRepository.AddAsync(songVideo);

        // Send push notification to all users
        try
        {
            var uploader = await _userManager.FindByIdAsync(createdByUserId);
            var uploaderName = uploader?.Nickname ?? uploader?.FirstName ?? "Um membro";
            var baseUrl = GetBaseUrl();

            var notification = _pushNotificationFactory.CreateSongVideoUploadNotification(
                song,
                uploaderName,
                baseUrl);

            // Get all users
            var allUserIds = await _userManager.Users
                .Select(u => u.Id)
                .ToListAsync();

            // Send to each user (excluding the uploader)
            foreach (var userId in allUserIds.Where(id => id != createdByUserId))
            {
                await _pushNotificationService.SendToUserAsync(userId, notification);
            }
        }
        catch (Exception ex)
        {
            // Log error but don't fail the operation
            _logger?.LogError(ex, "Failed to send push notifications for song video upload. SongId: {SongId}", songId);
        }

        return createdVideo;
    }

    public async Task DeleteVideoAsync(int videoId, string userId, bool isAdmin = false)
    {
        // Fetch video by id
        var video = await _songVideoRepository.GetByIdAsync(videoId);

        if (video == null)
            throw new EntityNotFoundException(nameof(SongVideo), videoId);

        // Check permissions: only allow if user is the uploader OR is an admin
        if (video.CreatedByUserId != userId && !isAdmin)
        {
            _logger?.LogWarning("User {UserId} attempted to delete video {VideoId} created by {CreatedByUserId} without permission",
                userId, videoId, video.CreatedByUserId);
            throw new UnauthorizedAccessException("You do not have permission to delete this video.");
        }

        // Delete from storage
        try
        {
            await _songVideoStorageService.DeleteVideoAsync(video.Url);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to delete video from storage: {VideoUrl}", video.Url);
            // Continue with database deletion even if storage deletion fails
        }

        // Delete from repository
        await _songVideoRepository.DeleteAsync(video);
    }

    public async Task<int> GetVideoCountBySongIdAsync(int songId)
    {
        return await _songVideoRepository.GetCountBySongIdAsync(songId);
    }

    // Play count tracking methods
    public async Task IncrementPlayCountAsync(int songId, string? userId = null)
    {
        var playCount = new SongPlayCount
        {
            SongId = songId,
            UserId = userId,
            PlayedAt = DateTime.UtcNow
        };

        _context.SongPlayCounts.Add(playCount);
        await _context.SaveChangesAsync();
    }

    public async Task<int> GetPlayCountBySongIdAsync(int songId)
    {
        return await _context.SongPlayCounts
            .Where(pc => pc.SongId == songId)
            .CountAsync();
    }

    public async Task<Dictionary<int, int>> GetPlayCountsForSongsAsync(IEnumerable<int> songIds)
    {
        var songIdsList = songIds.ToList();

        var playCounts = await _context.SongPlayCounts
            .Where(pc => songIdsList.Contains(pc.SongId))
            .GroupBy(pc => pc.SongId)
            .Select(g => new { SongId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SongId, x => x.Count);

        return playCounts;
    }

    public async Task<IEnumerable<(Song Song, int PlayCount)>> GetTopSongsAsync(int count = 10)
    {
        var topSongs = await _context.SongPlayCounts
            .GroupBy(pc => pc.SongId)
            .Select(g => new { SongId = g.Key, PlayCount = g.Count() })
            .OrderByDescending(x => x.PlayCount)
            .Take(count)
            .Join(
                _context.Songs.Include(s => s.Album),
                pc => pc.SongId,
                s => s.Id,
                (pc, s) => new { Song = s, pc.PlayCount }
            )
            .ToListAsync();

        return topSongs.Select(x => (x.Song, x.PlayCount));
    }

    public async Task<IEnumerable<(Album Album, int PlayCount)>> GetTopAlbumsAsync(int count = 10)
    {
        var topAlbums = await _context.SongPlayCounts
            .Join(
                _context.Songs,
                pc => pc.SongId,
                s => s.Id,
                (pc, s) => new { s.AlbumId, pc.Id }
            )
            .GroupBy(x => x.AlbumId)
            .Select(g => new { AlbumId = g.Key, PlayCount = g.Count() })
            .OrderByDescending(x => x.PlayCount)
            .Take(count)
            .Join(
                _context.Albums,
                pc => pc.AlbumId,
                a => a.Id,
                (pc, a) => new { Album = a, pc.PlayCount }
            )
            .ToListAsync();

        return topAlbums.Select(x => (x.Album, x.PlayCount));
    }

    public async Task<IEnumerable<(UserPlayInfo User, Song Song, Album Album, int PlayCount)>> GetDetailedPlayStatsAsync()
    {
        var stats = await _context.SongPlayCounts
            .Where(pc => pc.UserId != null)
            .GroupBy(pc => new { pc.UserId, pc.SongId })
            .Select(g => new
            {
                g.Key.UserId,
                g.Key.SongId,
                PlayCount = g.Count()
            })
            .Join(
                _context.Users,
                pc => pc.UserId,
                u => u.Id,
                (pc, u) => new
                {
                    pc.SongId,
                    User = new UserPlayInfo
                    {
                        UserId = u.Id,
                        Nickname = u.Nickname ?? "Unknown",
                        FirstName = u.FirstName ?? "",
                        LastName = u.LastName ?? "",
                        ProfilePictureSrc = !string.IsNullOrEmpty(u.ImageUrl) ? u.ImageUrl : "/images/default-avatar.webp"
                    },
                    pc.PlayCount
                }
            )
            .Join(
                _context.Songs.Include(s => s.Album),
                pc => pc.SongId,
                s => s.Id,
                (pc, s) => new { pc.User, Song = s, Album = s.Album, pc.PlayCount }
            )
            .Where(x => x.Album != null) // Filter out songs without albums
            .OrderByDescending(x => x.PlayCount)
            .ToListAsync();

        return stats.Select(x => (x.User, x.Song, x.Album!, x.PlayCount));
    }

    public async Task<IEnumerable<(Song Song, int PlayCount)>> GetAllSongsWithPlayCountAsync()
    {
        var songStats = await _context.SongPlayCounts
            .GroupBy(pc => pc.SongId)
            .Select(g => new { SongId = g.Key, PlayCount = g.Count() })
            .Join(
                _context.Songs.Include(s => s.Album),
                pc => pc.SongId,
                s => s.Id,
                (pc, s) => new { Song = s, pc.PlayCount }
            )
            .OrderByDescending(x => x.PlayCount)
            .ToListAsync();

        return songStats.Select(x => (x.Song, x.PlayCount));
    }

    public async Task<IEnumerable<(Album Album, int PlayCount)>> GetAllAlbumsWithPlayCountAsync()
    {
        var albumStats = await _context.SongPlayCounts
            .GroupBy(pc => pc.Song!.AlbumId)
            .Select(g => new { AlbumId = g.Key, PlayCount = g.Count() })
            .Join(
                _context.Albums,
                pc => pc.AlbumId,
                a => a.Id,
                (pc, a) => new { Album = a, pc.PlayCount }
            )
            .OrderByDescending(x => x.PlayCount)
            .ToListAsync();

        return albumStats.Select(x => (x.Album, x.PlayCount));
    }

    private string GetBaseUrl()
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        if (request != null)
        {
            return $"{request.Scheme}://{request.Host}";
        }
        return "https://rtub.pt"; // Fallback
    }
}
