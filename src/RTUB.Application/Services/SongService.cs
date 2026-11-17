using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Services;

/// <summary>
/// Song service implementation using Repository pattern
/// Contains business logic for song operations
/// Follows Single Responsibility and Dependency Inversion principles
/// Now depends on ISongRepository abstraction instead of concrete DbContext
/// </summary>
public partial class SongService : ISongService
{
    private readonly ISongRepository _songRepository;

    private readonly ISongVideoRepository _songVideoRepository;

    private readonly ISongVideoStorageService _songVideoStorageService;

    private readonly IAlbumRepository _albumRepository;

    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    private readonly ILogger<SongService>? _logger;

    private readonly IPushNotificationFactory _pushNotificationFactory;

    private readonly IPushNotificationService _pushNotificationService;

    private readonly UserManager<ApplicationUser> _userManager;

    public SongService(
        ISongRepository songRepository,
        ISongVideoRepository songVideoRepository,
        ISongVideoStorageService songVideoStorageService,
        IAlbumRepository albumRepository,
        IDbContextFactory<ApplicationDbContext> contextFactory,
        IPushNotificationFactory pushNotificationFactory,
        IPushNotificationService pushNotificationService,
        UserManager<ApplicationUser> userManager,
        ILogger<SongService>? logger = null)
    {
        _songRepository = songRepository;
        _songVideoRepository = songVideoRepository;
        _songVideoStorageService = songVideoStorageService;
        _albumRepository = albumRepository;
        _contextFactory = contextFactory;
        _logger = logger;
        _pushNotificationFactory = pushNotificationFactory;
        _pushNotificationService = pushNotificationService;
        _userManager = userManager;
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
}
