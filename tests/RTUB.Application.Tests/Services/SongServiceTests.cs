using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for SongService
/// Tests song CRUD operations and YouTube URL management
/// </summary>
public class SongServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly SongService _songService;
    private readonly AlbumService _albumService;
    private readonly Mock<IImageStorageService> _mockImageStorageService;

    public SongServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _songService = new SongService(_context);
        _mockImageStorageService = new Mock<IImageStorageService>();
        _albumService = new AlbumService(_context, _mockImageStorageService.Object);
    }

    [Fact]
    public async Task CreateSongAsync_WithValidData_CreatesSong()
    {
        // Arrange
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var title = "Beautiful Song";
        var trackNumber = 1;

        // Act
        var result = await _songService.CreateSongAsync(title, album.Id, trackNumber);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be(title);
        result.AlbumId.Should().Be(album.Id);
        result.TrackNumber.Should().Be(trackNumber);
    }

    [Fact]
    public async Task GetSongByIdAsync_ExistingSong_ReturnsSong()
    {
        // Arrange
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);

        // Act
        var result = await _songService.GetSongByIdAsync(song.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(song.Id);
        result.Title.Should().Be("Test Song");
    }

    [Fact]
    public async Task GetSongByIdAsync_NonExistingSong_ReturnsNull()
    {
        // Act
        var result = await _songService.GetSongByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllSongsAsync_WithMultipleSongs_ReturnsAll()
    {
        // Arrange
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        await _songService.CreateSongAsync("Song 1", album.Id);
        await _songService.CreateSongAsync("Song 2", album.Id);
        await _songService.CreateSongAsync("Song 3", album.Id);

        // Act
        var result = await _songService.GetAllSongsAsync();

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetSongsByAlbumIdAsync_ReturnsSongsOrderedByTrackNumber()
    {
        // Arrange
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        await _songService.CreateSongAsync("Song 3", album.Id, 3);
        await _songService.CreateSongAsync("Song 1", album.Id, 1);
        await _songService.CreateSongAsync("Song 2", album.Id, 2);

        // Act
        var result = (await _songService.GetSongsByAlbumIdAsync(album.Id)).ToList();

        // Assert
        result.Should().HaveCount(3);
        result[0].TrackNumber.Should().Be(1);
        result[1].TrackNumber.Should().Be(2);
        result[2].TrackNumber.Should().Be(3);
    }

    [Fact]
    public async Task UpdateSongAsync_UpdatesSongDetails()
    {
        // Arrange
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Original Title", album.Id);
        var newTitle = "Updated Title";
        var trackNumber = 5;
        var lyricAuthor = "John Doe";
        var musicAuthor = "Jane Smith";
        var adaptation = "RTUB";
        var duration = 240;

        // Act
        await _songService.UpdateSongAsync(song.Id, newTitle, trackNumber, lyricAuthor, musicAuthor, adaptation, duration);
        var updated = await _songService.GetSongByIdAsync(song.Id);

        // Assert
        updated!.Title.Should().Be(newTitle);
        updated.TrackNumber.Should().Be(trackNumber);
        updated.LyricAuthor.Should().Be(lyricAuthor);
        updated.MusicAuthor.Should().Be(musicAuthor);
        updated.Adaptation.Should().Be(adaptation);
        updated.Duration.Should().Be(duration);
    }

    [Fact]
    public async Task UpdateSongAsync_WithInvalidId_ThrowsException()
    {
        // Act & Assert
        var act = async () => await _songService.UpdateSongAsync(999, "Title", 1, "Author", "Composer", "Adaptation", 180);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task SetSongLyricsAsync_SetsLyrics()
    {
        // Arrange
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);
        var lyrics = "These are the lyrics...";

        // Act
        await _songService.SetSongLyricsAsync(song.Id, lyrics);
        var updated = await _songService.GetSongByIdAsync(song.Id);

        // Assert
        updated!.Lyrics.Should().Be(lyrics);
    }

    [Fact]
    public async Task SetSongSpotifyUrlAsync_SetsUrl()
    {
        // Arrange
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);
        var spotifyUrl = "https://open.spotify.com/track/abc123";

        // Act
        await _songService.SetSongSpotifyUrlAsync(song.Id, spotifyUrl);
        var updated = await _songService.GetSongByIdAsync(song.Id);

        // Assert
        updated!.SpotifyUrl.Should().Be(spotifyUrl);
    }

    [Fact]
    public async Task DeleteSongAsync_RemovesSong()
    {
        // Arrange
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);

        // Act
        await _songService.DeleteSongAsync(song.Id);
        var deleted = await _songService.GetSongByIdAsync(song.Id);

        // Assert
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteSongAsync_WithInvalidId_ThrowsException()
    {
        // Act & Assert
        var act = async () => await _songService.DeleteSongAsync(999);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task AddYouTubeUrlAsync_WithInvalidSongId_ThrowsException()
    {
        // Act & Assert
        var act = async () => await _songService.AddYouTubeUrlAsync(999, "https://youtube.com/watch?v=test");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task AddYouTubeUrlAsync_WithEmptyUrl_ThrowsException()
    {
        // Arrange
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);

        // Act & Assert
        var act = async () => await _songService.AddYouTubeUrlAsync(song.Id, "");
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*YouTube URL*");
    }

    [Fact]
    public async Task AddYouTubeUrlAsync_AddsUrlToSong()
    {
        // Arrange
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);
        var youtubeUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";

        // Act
        await _songService.AddYouTubeUrlAsync(song.Id, youtubeUrl);
        var updated = await _songService.GetSongByIdAsync(song.Id);

        // Assert
        updated.Should().NotBeNull();
        updated!.YouTubeUrls.Should().HaveCount(1);
        updated.YouTubeUrls.First().Url.Should().Be(youtubeUrl.ToLowerInvariant());
    }

    [Fact]
    public async Task AddYouTubeUrlAsync_PersistsAfterReload()
    {
        // Arrange
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);
        var youtubeUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";

        // Act
        await _songService.AddYouTubeUrlAsync(song.Id, youtubeUrl);
        
        // Simulate page refresh by fetching song again from database
        var reloaded = await _songService.GetSongByIdAsync(song.Id);

        // Assert
        reloaded.Should().NotBeNull();
        reloaded!.YouTubeUrls.Should().HaveCount(1);
        reloaded.YouTubeUrls.First().Url.Should().Be(youtubeUrl.ToLowerInvariant());
    }

    [Fact]
    public async Task AddYouTubeUrlAsync_WithMultipleUrls_AddsAll()
    {
        // Arrange
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);
        var url1 = "https://www.youtube.com/watch?v=url1";
        var url2 = "https://www.youtube.com/watch?v=url2";
        var url3 = "https://www.youtube.com/watch?v=url3";

        // Act
        await _songService.AddYouTubeUrlAsync(song.Id, url1);
        await _songService.AddYouTubeUrlAsync(song.Id, url2);
        await _songService.AddYouTubeUrlAsync(song.Id, url3);
        var updated = await _songService.GetSongByIdAsync(song.Id);

        // Assert
        updated!.YouTubeUrls.Should().HaveCount(3);
        updated.YouTubeUrls.Select(u => u.Url).Should().Contain(new[] {
            url1.ToLowerInvariant(),
            url2.ToLowerInvariant(),
            url3.ToLowerInvariant()
        });
    }

    [Fact]
    public async Task AddYouTubeUrlAsync_WithDuplicateUrl_DoesNotAddDuplicate()
    {
        // Arrange
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);
        var youtubeUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";

        // Act
        await _songService.AddYouTubeUrlAsync(song.Id, youtubeUrl);
        await _songService.AddYouTubeUrlAsync(song.Id, youtubeUrl); // Try to add again
        var updated = await _songService.GetSongByIdAsync(song.Id);

        // Assert
        updated!.YouTubeUrls.Should().HaveCount(1);
    }

    [Fact]
    public async Task AddYouTubeUrlAsync_WithDifferentCaseUrl_RecognizesAsDuplicate()
    {
        // Arrange
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);
        var url1 = "https://www.YouTube.com/watch?v=ABC123";
        var url2 = "https://www.youtube.com/watch?v=abc123";

        // Act
        await _songService.AddYouTubeUrlAsync(song.Id, url1);
        await _songService.AddYouTubeUrlAsync(song.Id, url2); // Different case but same URL
        var updated = await _songService.GetSongByIdAsync(song.Id);

        // Assert
        updated!.YouTubeUrls.Should().HaveCount(1);
    }

    [Fact]
    public async Task RemoveYouTubeUrlAsync_RemovesUrl()
    {
        // Arrange
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);
        var youtubeUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";
        await _songService.AddYouTubeUrlAsync(song.Id, youtubeUrl);

        // Act
        await _songService.RemoveYouTubeUrlAsync(song.Id, youtubeUrl);
        var updated = await _songService.GetSongByIdAsync(song.Id);

        // Assert
        updated!.YouTubeUrls.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveYouTubeUrlAsync_WithDifferentCase_RemovesUrl()
    {
        // Arrange
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);
        var url = "https://www.YouTube.com/watch?v=ABC123";
        await _songService.AddYouTubeUrlAsync(song.Id, url);

        // Act - try to remove with different case
        await _songService.RemoveYouTubeUrlAsync(song.Id, "https://www.youtube.com/watch?v=abc123");
        var updated = await _songService.GetSongByIdAsync(song.Id);

        // Assert
        updated!.YouTubeUrls.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveYouTubeUrlAsync_WithNonExistentUrl_DoesNotThrow()
    {
        // Arrange
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);

        // Act & Assert - should not throw
        await _songService.RemoveYouTubeUrlAsync(song.Id, "https://www.youtube.com/watch?v=nonexistent");
        var updated = await _songService.GetSongByIdAsync(song.Id);
        updated!.YouTubeUrls.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteSongAsync_CascadeDeletesYouTubeUrls()
    {
        // Arrange
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);
        await _songService.AddYouTubeUrlAsync(song.Id, "https://www.youtube.com/watch?v=url1");
        await _songService.AddYouTubeUrlAsync(song.Id, "https://www.youtube.com/watch?v=url2");

        // Act
        await _songService.DeleteSongAsync(song.Id);

        // Assert
        var deleted = await _songService.GetSongByIdAsync(song.Id);
        deleted.Should().BeNull();
        
        // Verify YouTube URLs are also deleted
        var orphanedUrls = await _context.SongYouTubeUrls
            .Where(u => u.SongId == song.Id)
            .ToListAsync();
        orphanedUrls.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllSongsAsync_IncludesYouTubeUrls()
    {
        // Arrange
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);
        await _songService.AddYouTubeUrlAsync(song.Id, "https://www.youtube.com/watch?v=test");

        // Act
        var songs = await _songService.GetAllSongsAsync();

        // Assert
        var retrieved = songs.First(s => s.Id == song.Id);
        retrieved.YouTubeUrls.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetSongsByAlbumIdAsync_IncludesYouTubeUrls()
    {
        // Arrange
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);
        await _songService.AddYouTubeUrlAsync(song.Id, "https://www.youtube.com/watch?v=test");

        // Act
        var songs = await _songService.GetSongsByAlbumIdAsync(album.Id);

        // Assert
        var retrieved = songs.First(s => s.Id == song.Id);
        retrieved.YouTubeUrls.Should().HaveCount(1);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
