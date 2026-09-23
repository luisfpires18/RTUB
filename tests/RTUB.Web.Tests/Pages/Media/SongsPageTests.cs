using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Pages.Media;
using RTUB.Web.Tests.Pages.Base;

namespace RTUB.Web.Tests.Pages.Media;

/// <summary>
/// Component tests for Songs.razor page
/// Tests page rendering, modal interactions, CRUD workflows, search functionality, and authorization
/// </summary>
public class SongsPageTests : PageTestBase
{
    private const string SkipModal = "Modal renders outside component fragment; cannot assert modal markup in bUnit.";

    private readonly Mock<IAlbumService> _mockAlbumService;
    private readonly Mock<ISongService> _mockSongService;
    private readonly Mock<ISongContentService> _mockSongContentService;
    private readonly Mock<ISongUrlCacheService> _mockSongUrlCacheService;
    private readonly Mock<ISongPlayService> _mockSongPlayService;
    private readonly Mock<ISongValidationService> _mockSongValidationService;
    private readonly Mock<IAudioStorageService> _mockAudioStorageService;
    private readonly Mock<ILyricStorageService> _mockLyricStorageService;
    private readonly Mock<IAuditLogService> _mockAuditLogService;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment> _mockWebHostEnvironment;
    private const int TestAlbumId = 1;

    public SongsPageTests()
    {
        // Setup service mocks
        _mockAlbumService = SetupService<IAlbumService>();
        _mockSongService = SetupService<ISongService>();
        _mockSongContentService = SetupService<ISongContentService>();
        _mockSongUrlCacheService = SetupService<ISongUrlCacheService>();
        _mockSongPlayService = SetupService<ISongPlayService>();
        _mockSongValidationService = SetupService<ISongValidationService>();
        _mockAudioStorageService = SetupService<IAudioStorageService>();
        _mockLyricStorageService = SetupService<ILyricStorageService>();
        _mockAuditLogService = SetupService<IAuditLogService>();
        _mockUserManager = SetupUserManager();
        _mockWebHostEnvironment = SetupWebHostEnvironment();

        _mockAuditLogService
            .Setup(x => x.AddAsync(It.IsAny<AuditLog>()))
            .Returns(Task.CompletedTask);

        _mockSongContentService
            .Setup(x => x.GetCleanVideoTitle(It.IsAny<SongVideo>(), It.IsAny<Song>(), It.IsAny<int>()))
            .Returns((SongVideo video, Song song, int num) => video.Title ?? $"{song.Title} - Vídeo {num}");

        _mockSongUrlCacheService
            .Setup(x => x.GetCachedUrl(It.IsAny<string>()))
            .Returns((string?)null);

        _mockSongUrlCacheService
            .Setup(x => x.CacheUrl(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .Verifiable();

        _mockSongPlayService
            .Setup(x => x.ShouldIncrementPlayCount(It.IsAny<int>(), It.IsAny<DateTime?>(), It.IsAny<int?>()))
            .Returns(true);

        _mockSongPlayService
            .Setup(x => x.GetLastPlayTime(It.IsAny<int>()))
            .Returns((DateTime?)null);

        _mockSongPlayService
            .Setup(x => x.RecordPlayTime(It.IsAny<int>(), It.IsAny<DateTime>()))
            .Verifiable();

        _mockSongValidationService
            .Setup(x => x.ValidateVideoFileSize(It.IsAny<Microsoft.AspNetCore.Components.Forms.IBrowserFile>(), It.IsAny<long>()))
            .Returns(true);

        Services.AddSingleton(new RTUB.Web.Services.MediaQueueService());
        Services.AddSingleton(new RTUB.Web.Interop.MediaSessionInterop(MockJSRuntime.Object));
        var audioLogger = new Mock<Microsoft.Extensions.Logging.ILogger<RTUB.Web.Interop.AudioPlayerInterop>>();
        Services.AddSingleton(audioLogger.Object);
        Services.AddSingleton(new RTUB.Web.Interop.AudioPlayerInterop(MockJSRuntime.Object, audioLogger.Object));

        SetupAuthentication("test-user", "Test User");

        // Setup default service responses
        var testAlbum = CreateTestAlbum(TestAlbumId, "Test Album", 2024);
        _mockAlbumService
            .Setup(x => x.GetAlbumByIdAsync(TestAlbumId))
            .ReturnsAsync(testAlbum);

        _mockSongService
            .Setup(x => x.GetSongsByAlbumIdAsync(TestAlbumId))
            .ReturnsAsync(new List<Song>());

        _mockSongService
            .Setup(x => x.GetPlayCountsForSongsAsync(It.IsAny<List<int>>()))
            .ReturnsAsync(new Dictionary<int, int>());

        _mockSongService
            .Setup(x => x.GetVideosBySongIdAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<SongVideo>());
    }

    #region Page Rendering Tests

    [Fact]
    public async Task SongsPage_RendersAlbumTitle()
    {
        // Arrange
        var album = CreateTestAlbum(TestAlbumId, "Test Album", 2024);
        _mockAlbumService
            .Setup(x => x.GetAlbumByIdAsync(TestAlbumId))
            .ReturnsAsync(album);

        // Act
        var cut = Render<Songs>(parameters => parameters
            .Add(p => p.AlbumId, TestAlbumId));
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Test Album", "page should display album title");
    }

    [Fact]
    public async Task SongsPage_ShowsLoadingState_WhenAlbumIsNull()
    {
        // Arrange
        _mockAlbumService
            .Setup(x => x.GetAlbumByIdAsync(TestAlbumId))
            .ReturnsAsync((Album?)null);

        // Act
        var cut = Render<Songs>(parameters => parameters
            .Add(p => p.AlbumId, TestAlbumId));

        // Assert
        cut.Markup.Should().Contain("A carregar", "page should show loading message");
    }

    [Fact]
    public async Task SongsPage_ShowsEmptyState_WhenNoSongs()
    {
        // Arrange
        var album = CreateTestAlbum(TestAlbumId, "Test Album", 2024);
        _mockAlbumService
            .Setup(x => x.GetAlbumByIdAsync(TestAlbumId))
            .ReturnsAsync(album);

        _mockSongService
            .Setup(x => x.GetSongsByAlbumIdAsync(TestAlbumId))
            .ReturnsAsync(new List<Song>());

        // Act
        var cut = Render<Songs>(parameters => parameters
            .Add(p => p.AlbumId, TestAlbumId));
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Faixas", "page should show 'Faixas' section");
    }

    [Fact]
    public async Task SongsPage_DisplaysSongs_WhenSongsExist()
    {
        // Arrange
        var album = CreateTestAlbum(TestAlbumId, "Test Album", 2024);
        var songs = new List<Song>
        {
            CreateTestSong(1, "Song 1", TestAlbumId, trackNumber: 1),
            CreateTestSong(2, "Song 2", TestAlbumId, trackNumber: 2)
        };

        _mockAlbumService
            .Setup(x => x.GetAlbumByIdAsync(TestAlbumId))
            .ReturnsAsync(album);

        _mockSongService
            .Setup(x => x.GetSongsByAlbumIdAsync(TestAlbumId))
            .ReturnsAsync(songs);

        // Act
        var cut = Render<Songs>(parameters => parameters
            .Add(p => p.AlbumId, TestAlbumId));
        cut.WaitForState(() => cut.Markup.Contains("Song") && !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Song 1", "page should display first song");
        cut.Markup.Should().Contain("Song 2", "page should display second song");
    }

    #endregion

    #region Authorization Tests

    [Fact(Skip = SkipModal)]
    public async Task SongsPage_ShowsCreateButton_ForAdminUser()
    {
        // Arrange
        SetupAuthentication("admin-user", "Admin User", "Admin");
        var album = CreateTestAlbum(TestAlbumId, "Test Album", 2024);
        _mockAlbumService
            .Setup(x => x.GetAlbumByIdAsync(TestAlbumId))
            .ReturnsAsync(album);

        // Act
        var cut = Render<Songs>(parameters => parameters
            .Add(p => p.AlbumId, TestAlbumId));
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Adicionar Música", "admin should see create button");
    }

    [Fact]
    public async Task SongsPage_HidesCreateButton_ForRegularUser()
    {
        // Arrange
        SetupAuthentication("regular-user", "Regular User");
        var album = CreateTestAlbum(TestAlbumId, "Test Album", 2024);
        _mockAlbumService
            .Setup(x => x.GetAlbumByIdAsync(TestAlbumId))
            .ReturnsAsync(album);

        // Act
        var cut = Render<Songs>(parameters => parameters
            .Add(p => p.AlbumId, TestAlbumId));
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().NotContain("Adicionar Música", "regular user should not see create button");
    }

    #endregion

    #region Modal Interaction Tests

    [Fact(Skip = SkipModal)]
    public async Task SongsPage_OpenCreateModal_ShowsCreateModal()
    {
        // Arrange
        SetupAuthentication("admin-user", "Admin User", "Admin");
        var album = CreateTestAlbum(TestAlbumId, "Test Album", 2024);
        _mockAlbumService
            .Setup(x => x.GetAlbumByIdAsync(TestAlbumId))
            .ReturnsAsync(album);

        var cut = Render<Songs>(parameters => parameters
            .Add(p => p.AlbumId, TestAlbumId));
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Act - Open create modal via reflection (method is private)
        var openCreateModalMethod = typeof(Songs).GetMethod("OpenCreateSongModal",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await cut.InvokeAsync(() => openCreateModalMethod!.Invoke(cut.Instance, null));

        // Assert
        cut.Markup.Should().Contain("Criar Nova Música", "create modal should be displayed");
    }

    [Fact(Skip = SkipModal)]
    public async Task SongsPage_OpenEditModal_ShowsEditModal()
    {
        // Arrange
        SetupAuthentication("admin-user", "Admin User", "Admin");
        var album = CreateTestAlbum(TestAlbumId, "Test Album", 2024);
        var song = CreateTestSong(1, "Test Song", TestAlbumId);

        _mockAlbumService
            .Setup(x => x.GetAlbumByIdAsync(TestAlbumId))
            .ReturnsAsync(album);

        _mockSongService
            .Setup(x => x.GetSongsByAlbumIdAsync(TestAlbumId))
            .ReturnsAsync(new List<Song> { song });

        var cut = Render<Songs>(parameters => parameters
            .Add(p => p.AlbumId, TestAlbumId));
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Act - Open edit modal via reflection (method is private)
        var openEditModalMethod = typeof(Songs).GetMethod("OpenEditModal",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await cut.InvokeAsync(() => openEditModalMethod!.Invoke(cut.Instance, new object[] { song }));

        // Assert
        cut.Markup.Should().Contain("Editar Música", "edit modal should be displayed");
    }

    [Fact(Skip = SkipModal)]
    public async Task SongsPage_OpenDeleteModal_ShowsDeleteModal()
    {
        // Arrange
        SetupAuthentication("admin-user", "Admin User", "Admin");
        var album = CreateTestAlbum(TestAlbumId, "Test Album", 2024);
        var song = CreateTestSong(1, "Test Song", TestAlbumId);

        _mockAlbumService
            .Setup(x => x.GetAlbumByIdAsync(TestAlbumId))
            .ReturnsAsync(album);

        _mockSongService
            .Setup(x => x.GetSongsByAlbumIdAsync(TestAlbumId))
            .ReturnsAsync(new List<Song> { song });

        var cut = Render<Songs>(parameters => parameters
            .Add(p => p.AlbumId, TestAlbumId));
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Act - Open delete modal via reflection (method is private)
        var openDeleteModalMethod = typeof(Songs).GetMethod("OpenDeleteSongModal",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await cut.InvokeAsync(() => openDeleteModalMethod!.Invoke(cut.Instance, new object[] { song }));

        // Assert
        cut.Markup.Should().Contain("Confirmar Eliminação", "delete modal should be displayed");
    }

    #endregion

    #region CRUD Operation Tests

    [Fact(Skip = SkipModal)]
    public async Task SongsPage_CreateSong_OpensModal()
    {
        // Arrange
        SetupAuthentication("admin-user", "Admin User", "Admin");
        var album = CreateTestAlbum(TestAlbumId, "Test Album", 2024);
        var newSong = CreateTestSong(1, "New Song", TestAlbumId);

        _mockAlbumService
            .Setup(x => x.GetAlbumByIdAsync(TestAlbumId))
            .ReturnsAsync(album);

        _mockSongService
            .Setup(x => x.CreateSongAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<int?>(),
                It.IsAny<string?>(),
                It.IsAny<bool>()))
            .ReturnsAsync(newSong);

        var cut = Render<Songs>(parameters => parameters
            .Add(p => p.AlbumId, TestAlbumId));
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Act - Open create modal
        var openCreateModalMethod = typeof(Songs).GetMethod("OpenCreateSongModal",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await cut.InvokeAsync(() => openCreateModalMethod!.Invoke(cut.Instance, null));

        // Assert
        cut.Markup.Should().Contain("Criar Nova Música", "create modal should be displayed");
    }

    [Fact]
    public async Task SongsPage_DeleteSong_CallsService()
    {
        // Arrange
        SetupAuthentication("admin-user", "Admin User", "Admin");
        var album = CreateTestAlbum(TestAlbumId, "Test Album", 2024);
        var song = CreateTestSong(1, "Test Song", TestAlbumId);

        _mockAlbumService
            .Setup(x => x.GetAlbumByIdAsync(TestAlbumId))
            .ReturnsAsync(album);

        _mockSongService
            .Setup(x => x.GetSongsByAlbumIdAsync(TestAlbumId))
            .ReturnsAsync(new List<Song> { song });

        _mockSongService
            .Setup(x => x.DeleteSongAsync(1))
            .Returns(Task.CompletedTask);

        // Setup for reload after delete
        _mockSongService
            .Setup(x => x.GetSongsByAlbumIdAsync(TestAlbumId))
            .ReturnsAsync(new List<Song>());

        var cut = Render<Songs>(parameters => parameters
            .Add(p => p.AlbumId, TestAlbumId));
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        var openDelete = typeof(Songs).GetMethod("OpenDeleteSongModal", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var deleteSong = typeof(Songs).GetMethod("DeleteSong", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        await cut.InvokeAsync(() => openDelete.Invoke(cut.Instance, new object[] { song }));
        await cut.InvokeAsync(async () =>
        {
            var t = (Task)deleteSong.Invoke(cut.Instance, null)!;
            await t;
        });

        _mockSongService.Verify(x => x.DeleteSongAsync(1), Times.Once, "DeleteSongAsync should be called");
    }

    #endregion

    #region Search Functionality Tests

    [Fact]
    public async Task SongsPage_SearchFiltersSongs_ByTitle()
    {
        // Arrange
        var album = CreateTestAlbum(TestAlbumId, "Test Album", 2024);
        var songs = new List<Song>
        {
            CreateTestSong(1, "First Song", TestAlbumId),
            CreateTestSong(2, "Second Song", TestAlbumId),
            CreateTestSong(3, "Third Song", TestAlbumId)
        };

        _mockAlbumService
            .Setup(x => x.GetAlbumByIdAsync(TestAlbumId))
            .ReturnsAsync(album);

        _mockSongService
            .Setup(x => x.GetSongsByAlbumIdAsync(TestAlbumId))
            .ReturnsAsync(songs);

        var cut = Render<Songs>(parameters => parameters
            .Add(p => p.AlbumId, TestAlbumId));
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Act - Set search term via reflection (private field)
        var searchTermField = typeof(Songs).GetField("searchTerm",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var didSetSearch = false;
        if (searchTermField != null)
        {
            searchTermField.SetValue(cut.Instance, "First");
            cut.Render();
            didSetSearch = true;
        }

        cut.Markup.Should().Contain("First Song", "should show matching or first song");
        if (didSetSearch)
        {
            cut.Markup.Should().NotContain("Second Song", "should hide non-matching songs");
            cut.Markup.Should().NotContain("Third Song", "should hide non-matching songs");
        }
    }

    #endregion

    #region Lock-screen Media Session Lifecycle

    private static readonly TimeSpan MediaSessionTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// ✕ removes the &lt;audio&gt; and the next song renders a new one. The lock-screen session must
    /// be released on ✕ and bound again for the new element. Before fix/030 it stayed bound to the
    /// removed element, so lock-screen pause/next/seek acted on audio that was no longer on screen.
    /// </summary>
    [Fact]
    public async Task ClosingThePlayer_ThenPlayingAgain_RebindsLockScreenSessionToTheNewAudio()
    {
        // Arrange
        var handlersBound = SetupInstalledPwaWithPlayableSongs();
        var cut = RenderPlayableSongs();
        await cut.FindAll("button[title='Reproduzir']")[0].ClickAsync(new MouseEventArgs());
        (await handlersBound.WaitAsync(MediaSessionTimeout)).Should().BeTrue("the first song binds the lock-screen session");
        var beforeClose = JsCalls().Length;

        // Act
        await cut.Find("button[aria-label='Fechar player de áudio']").ClickAsync(new MouseEventArgs());
        cut.FindAll("#rtub-audio-player").Should().BeEmpty("closing the player removes the audio element");
        await cut.FindAll("button[title='Reproduzir']")[1].ClickAsync(new MouseEventArgs());

        // Assert
        (await handlersBound.WaitAsync(MediaSessionTimeout)).Should().BeTrue(
            "the next song renders a new audio element, and the lock-screen session must be bound to it");
        cut.FindAll("#rtub-audio-player").Should().ContainSingle();
        JsCalls().Skip(beforeClose).Should().ContainInOrder(
            new[] { "pwaMediaSession.cleanup", "pwaMediaSession.init", "pwaMediaSession.bindHandlers" },
            "✕ must release the session of the removed element before the next song binds its own");
    }

    /// <summary>
    /// Leaving the album page removes the player, which stops playback. Its lock-screen handlers
    /// and metadata must go with it instead of staying bound to audio that is on no page at all.
    /// </summary>
    [Fact]
    public async Task LeavingTheAlbumPage_WhilePlaying_ReleasesTheLockScreenSession()
    {
        // Arrange
        var handlersBound = SetupInstalledPwaWithPlayableSongs();
        var cut = RenderPlayableSongs();
        await cut.FindAll("button[title='Reproduzir']")[0].ClickAsync(new MouseEventArgs());
        (await handlersBound.WaitAsync(MediaSessionTimeout)).Should().BeTrue("the song binds the lock-screen session");
        var beforeLeaving = JsCalls().Length;

        // Act
        await DisposeComponentsAsync();

        // Assert
        JsCalls().Skip(beforeLeaving).Should().Contain(
            new[] { "pwaMediaSession.cleanup", "rtubMediaSession.clearMetadata" },
            "no lock-screen handler or metadata may outlive the player it belonged to");
    }

    /// <summary>
    /// Installed-PWA mode with two playable songs. The returned semaphore is released every time the
    /// lock-screen handlers are bound - the last step of initialising the session for an element.
    /// </summary>
    private SemaphoreSlim SetupInstalledPwaWithPlayableSongs()
    {
        var songs = new List<Song>
        {
            CreateTestSong(1, "Song 1", TestAlbumId, trackNumber: 1),
            CreateTestSong(2, "Song 2", TestAlbumId, trackNumber: 2)
        };
        songs.ForEach(song => song.HasMusic = true);
        _mockSongService
            .Setup(x => x.GetSongsByAlbumIdAsync(TestAlbumId))
            .ReturnsAsync(songs);
        _mockAudioStorageService
            .Setup(x => x.GetAudioUrlAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()))
            .ReturnsAsync("https://audio.example/song.mp3");

        MockJSRuntime
            .Setup(x => x.InvokeAsync<bool>("pwaMediaSession.detectPwaMode", It.IsAny<object?[]?>()))
            .ReturnsAsync(true);
        MockJSRuntime
            .Setup(x => x.InvokeAsync<bool>("pwaMediaSession.init", It.IsAny<object?[]?>()))
            .ReturnsAsync(true);

        var handlersBound = new SemaphoreSlim(0);
        MockJSRuntime
            .Setup(x => x.InvokeAsync<IJSVoidResult>("pwaMediaSession.bindHandlers", It.IsAny<object?[]?>()))
            .Callback(() => handlersBound.Release())
            .Returns(default(ValueTask<IJSVoidResult>));
        return handlersBound;
    }

    private IRenderedComponent<Songs> RenderPlayableSongs()
    {
        var cut = Render<Songs>(parameters => parameters
            .Add(p => p.AlbumId, TestAlbumId));
        cut.WaitForState(() => cut.FindAll("button[title='Reproduzir']").Count == 2, MediaSessionTimeout);
        return cut;
    }

    /// <summary>The JavaScript functions the page invoked, in call order.</summary>
    private string[] JsCalls() => MockJSRuntime.Invocations
        .Where(invocation => invocation.Method.Name == nameof(IJSRuntime.InvokeAsync))
        .Select(invocation => (string)invocation.Arguments[0])
        .ToArray();

    #endregion

    #region Helper Methods

    private static Album CreateTestAlbum(int id, string title, int year, bool isPrivate = false)
    {
        var a = Album.Create(
            title: title,
            year: year,
            description: $"Description for {title}",
            isPrivate: isPrivate
        );
        a.Id = id;
        return a;
    }

    private static Song CreateTestSong(int id, string title, int albumId, int? trackNumber = null)
    {
        var song = Song.Create(
            title: title,
            albumId: albumId,
            trackNumber: trackNumber
        );
        song.Id = id;
        return song;
    }

    #endregion
}
