using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Application.Interfaces;
using Moq;

namespace RTUB.Integration.Tests;

/// <summary>
/// Integration tests for YouTube URL persistence in Song management
/// Tests the full lifecycle of YouTube URLs from creation to retrieval
/// </summary>
public class SongYouTubeUrlPersistenceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly SongService _songService;
    private readonly AlbumService _albumService;
    private readonly Mock<IImageStorageService> _mockImageStorageService;

    public SongYouTubeUrlPersistenceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options, Mock.Of<Microsoft.AspNetCore.Http.IHttpContextAccessor>(), new AuditContext());
        _songService = new SongService(_context);
        _mockImageStorageService = new Mock<IImageStorageService>();
        _albumService = new AlbumService(_context, _mockImageStorageService.Object);
    }

    [Fact]
    public async Task YouTubeUrls_PersistAcrossContexts()
    {
        // Arrange
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);
        var url1 = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";
        var url2 = "https://www.youtube.com/watch?v=abc123";

        // Act - Add URLs
        await _songService.AddYouTubeUrlAsync(song.Id, url1);
        await _songService.AddYouTubeUrlAsync(song.Id, url2);

        // Detach all entities to simulate new context
        _context.ChangeTracker.Clear();

        // Retrieve song in fresh context
        var retrievedSong = await _songService.GetSongByIdAsync(song.Id);

        // Assert
        retrievedSong.Should().NotBeNull();
        retrievedSong!.YouTubeUrls.Should().HaveCount(2);
        retrievedSong.YouTubeUrls.Select(u => u.Url).Should().Contain(new[] {
            url1.ToLowerInvariant(),
            url2.ToLowerInvariant()
        });
    }

    [Fact]
    public async Task YouTubeUrls_UpdatePersistsCorrectly()
    {
        // Arrange
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);
        var oldUrl = "https://www.youtube.com/watch?v=old";
        var newUrl = "https://www.youtube.com/watch?v=new";

        // Act - Add old URL
        await _songService.AddYouTubeUrlAsync(song.Id, oldUrl);
        _context.ChangeTracker.Clear();

        // Verify old URL exists
        var songWithOldUrl = await _songService.GetSongByIdAsync(song.Id);
        songWithOldUrl!.YouTubeUrls.Should().HaveCount(1);
        songWithOldUrl.YouTubeUrls.First().Url.Should().Be(oldUrl.ToLowerInvariant());

        // Remove old and add new
        await _songService.RemoveYouTubeUrlAsync(song.Id, oldUrl);
        await _songService.AddYouTubeUrlAsync(song.Id, newUrl);
        _context.ChangeTracker.Clear();

        // Verify update persisted
        var updatedSong = await _songService.GetSongByIdAsync(song.Id);

        // Assert
        updatedSong!.YouTubeUrls.Should().HaveCount(1);
        updatedSong.YouTubeUrls.First().Url.Should().Be(newUrl.ToLowerInvariant());
    }

    [Fact]
    public async Task GetAllSongsAsync_LoadsYouTubeUrlsForAllSongs()
    {
        // Arrange
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song1 = await _songService.CreateSongAsync("Song 1", album.Id, 1);
        var song2 = await _songService.CreateSongAsync("Song 2", album.Id, 2);
        
        await _songService.AddYouTubeUrlAsync(song1.Id, "https://www.youtube.com/watch?v=song1");
        await _songService.AddYouTubeUrlAsync(song2.Id, "https://www.youtube.com/watch?v=song2a");
        await _songService.AddYouTubeUrlAsync(song2.Id, "https://www.youtube.com/watch?v=song2b");

        // Act
        _context.ChangeTracker.Clear();
        var allSongs = await _songService.GetAllSongsAsync();

        // Assert
        var retrievedSong1 = allSongs.First(s => s.Id == song1.Id);
        var retrievedSong2 = allSongs.First(s => s.Id == song2.Id);

        retrievedSong1.YouTubeUrls.Should().HaveCount(1);
        retrievedSong2.YouTubeUrls.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetSongsByAlbumIdAsync_LoadsYouTubeUrlsForAllAlbumSongs()
    {
        // Arrange
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song1 = await _songService.CreateSongAsync("Song 1", album.Id, 1);
        var song2 = await _songService.CreateSongAsync("Song 2", album.Id, 2);
        
        await _songService.AddYouTubeUrlAsync(song1.Id, "https://www.youtube.com/watch?v=song1");
        await _songService.AddYouTubeUrlAsync(song2.Id, "https://www.youtube.com/watch?v=song2");

        // Act
        _context.ChangeTracker.Clear();
        var albumSongs = await _songService.GetSongsByAlbumIdAsync(album.Id);

        // Assert
        var songsWithUrls = albumSongs.Where(s => s.YouTubeUrls.Any()).ToList();
        songsWithUrls.Should().HaveCount(2);
    }

    [Fact]
    public async Task NonDestructiveUpdate_PreservesExistingYouTubeUrls()
    {
        // Arrange
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);
        var url = "https://www.youtube.com/watch?v=test";
        await _songService.AddYouTubeUrlAsync(song.Id, url);

        // Act - Update song metadata (non-destructive)
        await _songService.UpdateSongAsync(
            song.Id,
            "Updated Title",
            1,
            "New Author",
            "New Composer",
            "New Adaptation",
            240
        );

        _context.ChangeTracker.Clear();
        var updatedSong = await _songService.GetSongByIdAsync(song.Id);

        // Assert - YouTube URLs should still be there
        updatedSong!.YouTubeUrls.Should().HaveCount(1);
        updatedSong.YouTubeUrls.First().Url.Should().Be(url.ToLowerInvariant());
    }

    [Fact]
    public async Task NonDestructiveUpdate_PreservesYouTubeUrlsAfterLyricsUpdate()
    {
        // Arrange
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);
        var url = "https://www.youtube.com/watch?v=test";
        await _songService.AddYouTubeUrlAsync(song.Id, url);

        // Act - Update lyrics (non-destructive)
        await _songService.SetSongLyricsAsync(song.Id, "New lyrics here...");

        _context.ChangeTracker.Clear();
        var updatedSong = await _songService.GetSongByIdAsync(song.Id);

        // Assert - YouTube URLs should still be there
        updatedSong!.YouTubeUrls.Should().HaveCount(1);
        updatedSong.YouTubeUrls.First().Url.Should().Be(url.ToLowerInvariant());
    }

    [Fact]
    public async Task NonDestructiveUpdate_PreservesYouTubeUrlsAfterSpotifyUpdate()
    {
        // Arrange
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);
        var url = "https://www.youtube.com/watch?v=test";
        await _songService.AddYouTubeUrlAsync(song.Id, url);

        // Act - Update Spotify URL (non-destructive)
        await _songService.SetSongSpotifyUrlAsync(song.Id, "https://open.spotify.com/track/abc");

        _context.ChangeTracker.Clear();
        var updatedSong = await _songService.GetSongByIdAsync(song.Id);

        // Assert - YouTube URLs should still be there
        updatedSong!.YouTubeUrls.Should().HaveCount(1);
        updatedSong.YouTubeUrls.First().Url.Should().Be(url.ToLowerInvariant());
    }

    [Fact]
    public async Task CascadeDelete_RemovesYouTubeUrlsWhenSongDeleted()
    {
        // Arrange
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);
        await _songService.AddYouTubeUrlAsync(song.Id, "https://www.youtube.com/watch?v=test1");
        await _songService.AddYouTubeUrlAsync(song.Id, "https://www.youtube.com/watch?v=test2");

        var songId = song.Id;

        // Act - Delete song
        await _songService.DeleteSongAsync(songId);
        _context.ChangeTracker.Clear();

        // Assert - Song and YouTube URLs should be gone
        var deletedSong = await _songService.GetSongByIdAsync(songId);
        deletedSong.Should().BeNull();

        var orphanedUrls = await _context.SongYouTubeUrls
            .Where(u => u.SongId == songId)
            .ToListAsync();
        orphanedUrls.Should().BeEmpty();
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
