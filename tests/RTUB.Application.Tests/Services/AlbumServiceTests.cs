using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for AlbumService
/// Tests album CRUD operations
/// </summary>
public class AlbumServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly AlbumService _albumService;
    private readonly Mock<IImageService> _mockImageService;
    private readonly Mock<IAlbumStorageService> _mockAlbumStorageService;
    private readonly Mock<ILogger<AlbumService>> _mockLogger;

    public AlbumServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _mockImageService = new Mock<IImageService>();
        _mockAlbumStorageService = new Mock<IAlbumStorageService>();
        _mockLogger = new Mock<ILogger<AlbumService>>();
        _albumService = new AlbumService(_context, _mockImageService.Object, _mockAlbumStorageService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task CreateAlbumAsync_WithValidData_CreatesAlbum()
    {
        // Arrange
        var title = "Greatest Hits";
        var year = 2020;
        var description = "Our best songs";

        // Act
        var result = await _albumService.CreateAlbumAsync(title, year, description);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be(title);
        result.Year.Should().Be(year);
        result.Description.Should().Be(description);
    }

    [Fact]
    public async Task GetAlbumByIdAsync_ExistingAlbum_ReturnsAlbum()
    {
        // Arrange
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);

        // Act
        var result = await _albumService.GetAlbumByIdAsync(album.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(album.Id);
        result.Title.Should().Be("Test Album");
    }

    [Fact]
    public async Task GetAlbumByIdAsync_NonExistingAlbum_ReturnsNull()
    {
        // Act
        var result = await _albumService.GetAlbumByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAlbumsAsync_WithMultipleAlbums_ReturnsAll()
    {
        // Arrange
        await _albumService.CreateAlbumAsync("Album 1", 2020);
        await _albumService.CreateAlbumAsync("Album 2", 2021);
        await _albumService.CreateAlbumAsync("Album 3", 2022);

        // Act
        var result = await _albumService.GetAllAlbumsAsync();

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetAlbumsWithSongsAsync_IncludesSongs()
    {
        // Arrange
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);

        // Act
        var result = await _albumService.GetAlbumsWithSongsAsync();

        // Assert
        result.Should().NotBeEmpty();
        result.First().Songs.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAlbumWithSongsAsync_ExistingAlbum_ReturnAlbumWithSongs()
    {
        // Arrange
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);

        // Act
        var result = await _albumService.GetAlbumWithSongsAsync(album.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Songs.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateAlbumAsync_UpdatesAlbumDetails()
    {
        // Arrange
        var album = await _albumService.CreateAlbumAsync("Original Title", 2020);
        var newTitle = "Updated Title";
        var newYear = 2021;
        var newDescription = "Updated description";

        // Act
        await _albumService.UpdateAlbumAsync(album.Id, newTitle, newYear, newDescription);
        var updated = await _albumService.GetAlbumByIdAsync(album.Id);

        // Assert
        updated!.Title.Should().Be(newTitle);
        updated.Year.Should().Be(newYear);
        updated.Description.Should().Be(newDescription);
    }

    [Fact]
    public async Task UpdateAlbumAsync_WithInvalidId_ThrowsException()
    {
        // Act & Assert
        var act = async () => await _albumService.UpdateAlbumAsync(999, "Title", 2020, "Description");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task DeleteAlbumAsync_RemovesAlbum()
    {
        // Arrange
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);

        // Act
        await _albumService.DeleteAlbumAsync(album.Id);
        var deleted = await _albumService.GetAlbumByIdAsync(album.Id);

        // Assert
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAlbumAsync_WithInvalidId_ThrowsException()
    {
        // Act & Assert
        var act = async () => await _albumService.DeleteAlbumAsync(999);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task SetAlbumCoverAsync_SetsImageUrl()
    {
        // Arrange
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var url = "https://example.com/cover.jpg";

        // Act
        await _albumService.SetAlbumCoverAsync(album.Id, null, null, url);
        var updated = await _albumService.GetAlbumByIdAsync(album.Id);

        // Assert
        updated!.CoverImageUrl.Should().Be(url);
    }

    [Fact]
    public async Task SetAlbumCoverAsync_WithInvalidId_ThrowsException()
    {
        // Arrange
        var url = "https://example.com/cover.jpg";

        // Act & Assert
        var act = async () => await _albumService.SetAlbumCoverAsync(999, null, null, url);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
