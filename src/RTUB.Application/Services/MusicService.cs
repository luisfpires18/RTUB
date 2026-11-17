using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// The React Music area (React track 006, docs/react-music.md): viewer-aware reads, the play
/// cooldown, and writes that go through the existing <see cref="IAlbumService"/> and
/// <see cref="ISongService"/> so audit logging, storage clean-up and video push notifications stay
/// exactly where they were. No schema change: everything maps onto the existing Music tables.
/// </summary>
public sealed partial class MusicService : IMusicService
{
    public const int DefaultCooldownSeconds = 30;

    public const long MaxCoverBytes = 5 * 1024 * 1024;

    public const long MaxVideoBytes = 100 * 1024 * 1024;

    private const int MaxYouTubeUrls = 10;

    private static readonly string[] CoverTypes = { "image/webp", "image/jpeg", "image/png" };

    private static readonly string[] VideoExtensions = { ".mp4", ".mov", ".m4v", ".webm", ".3gp", ".mkv", ".avi" };

    // ponytail: one in-process lock and IMemoryCache, so the cooldown is per app instance; a
    // scaled-out App Service would need a shared cache to stop a listener spreading plays across instances.
    private static readonly object CooldownGate = new();

    private readonly IDbContextFactory<ApplicationDbContext> _contexts;

    private readonly IAlbumService _albums;

    private readonly ISongService _songs;

    private readonly IAudioStorageService _audio;

    private readonly ILyricStorageService _lyrics;

    private readonly IImageStorageService _images;

    private readonly ISongContentService _songContent;

    private readonly IAuditLogService _audit;

    private readonly IMemoryCache _cache;

    private readonly ILogger<MusicService> _logger;

    private readonly IPushNotificationService _push;

    private readonly IPushNotificationFactory _pushFactory;

    public MusicService(
        IDbContextFactory<ApplicationDbContext> contexts,
        IAlbumService albums,
        ISongService songs,
        IAudioStorageService audio,
        ILyricStorageService lyrics,
        IImageStorageService images,
        ISongContentService songContent,
        IAuditLogService audit,
        IMemoryCache cache,
        ILogger<MusicService> logger,
        IPushNotificationService push,
        IPushNotificationFactory pushFactory)
    {
        _push = push;
        _pushFactory = pushFactory;
        _contexts = contexts;
        _albums = albums;
        _songs = songs;
        _audio = audio;
        _lyrics = lyrics;
        _images = images;
        _songContent = songContent;
        _audit = audit;
        _cache = cache;
        _logger = logger;
    }
}
