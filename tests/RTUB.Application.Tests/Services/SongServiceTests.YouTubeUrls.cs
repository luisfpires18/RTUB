// SongService: you tube urls.
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Tests.Services;

public partial class SongServiceTests
{
    [Fact]
    public async Task AddYouTubeUrlAsync_WithInvalidSongId_ThrowsException()
    {
        // Act & Assert
        var act = async () => await _songService.AddYouTubeUrlAsync(999, "https://youtube.com/watch?v=test");
        await act.Should().ThrowAsync<EntityNotFoundException>()
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
        var updated = await _context.Songs.AsNoTracking().Include(s => s.YouTubeUrls).FirstOrDefaultAsync(s => s.Id == song.Id);

        // Assert
        updated.Should().NotBeNull();
        updated!.YouTubeUrls.Should().HaveCount(1);
        updated.YouTubeUrls.First().Url.Should().Be(youtubeUrl);
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
        var reloaded = await _context.Songs.AsNoTracking().Include(s => s.YouTubeUrls).FirstOrDefaultAsync(s => s.Id == song.Id);

        // Assert
        reloaded.Should().NotBeNull();
        reloaded!.YouTubeUrls.Should().HaveCount(1);
        reloaded.YouTubeUrls.First().Url.Should().Be(youtubeUrl);
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
        var updated = await _context.Songs.AsNoTracking().Include(s => s.YouTubeUrls).FirstOrDefaultAsync(s => s.Id == song.Id);

        // Assert
        updated!.YouTubeUrls.Should().HaveCount(3);
        updated.YouTubeUrls.Select(u => u.Url).Should().Contain(new[] {
            url1,
            url2,
            url3
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
        var updated = await _context.Songs.AsNoTracking().Include(s => s.YouTubeUrls).FirstOrDefaultAsync(s => s.Id == song.Id);

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
        var updated = await _context.Songs.AsNoTracking().Include(s => s.YouTubeUrls).FirstOrDefaultAsync(s => s.Id == song.Id);

        // Assert
        updated!.YouTubeUrls.Should().HaveCount(1);
    }

    [Fact]
    public async Task AddYouTubeUrlAsync_PreservesOriginalCaseInUrl()
    {
        // Arrange
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);
        var youtubeUrl = "https://www.youtube.com/watch?v=A1RkcBlAqmU";

        // Act
        await _songService.AddYouTubeUrlAsync(song.Id, youtubeUrl);
        var updated = await _context.Songs.AsNoTracking().Include(s => s.YouTubeUrls).FirstOrDefaultAsync(s => s.Id == song.Id);

        // Assert - URL should preserve exact original casing
        updated.Should().NotBeNull();
        updated!.YouTubeUrls.Should().HaveCount(1);
        updated.YouTubeUrls.First().Url.Should().Be(youtubeUrl);
        updated.YouTubeUrls.First().Url.Should().Contain("A1RkcBlAqmU");
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
        var updated = await _context.Songs.AsNoTracking().Include(s => s.YouTubeUrls).FirstOrDefaultAsync(s => s.Id == song.Id);

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
        var updated = await _context.Songs.AsNoTracking().Include(s => s.YouTubeUrls).FirstOrDefaultAsync(s => s.Id == song.Id);

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
        var updated = await _context.Songs.AsNoTracking().Include(s => s.YouTubeUrls).FirstOrDefaultAsync(s => s.Id == song.Id);
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
        var deleted = await _context.Songs.AsNoTracking().Include(s => s.YouTubeUrls).FirstOrDefaultAsync(s => s.Id == song.Id);
        deleted.Should().BeNull();

        // Clear tracked entities so _context re-reads from the InMemory store
        _context.ChangeTracker.Clear();

        // Verify YouTube URLs are also deleted
        var orphanedUrls = await _context.SongYouTubeUrls
            .Where(u => u.SongId == song.Id)
            .ToListAsync();
        orphanedUrls.Should().BeEmpty();
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
}
