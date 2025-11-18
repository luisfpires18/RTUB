using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for AlbumService
/// Tests album CRUD operations
/// </summary>
public class AlbumServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly AlbumService _albumService;
    private readonly Mock<IImageStorageService> _mockImageStorageService;

    public AlbumServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options, Mock.Of<Microsoft.AspNetCore.Http.IHttpContextAccessor>(), new AuditContext());
        _mockImageStorageService = new Mock<IImageStorageService>();
        _albumService = new AlbumService(_context, _mockImageStorageService.Object);
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
        await _albumService.UpdateAlbumAsync(album.Id, newTitle, newYear, newDescription, false);
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
        var act = async () => await _albumService.UpdateAlbumAsync(999, "Title", 2020, "Description", false);
        await act.Should().ThrowAsync<EntityNotFoundException>()
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
        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task UpdateAlbumWithCoverAsync_UpdatesAlbumDetailsAndCover()
    {
        // Arrange
        var album = await _albumService.CreateAlbumAsync("Original Title", 2020);
        var newTitle = "Updated Title";
        var newYear = 2021;
        var newDescription = "Updated description";
        var imageUrl = "https://example.com/test-image.webp";
        
        _mockImageStorageService
            .Setup(x => x.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(imageUrl);

        // Act
        using var imageStream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        await _albumService.UpdateAlbumWithCoverAsync(album.Id, newTitle, newYear, newDescription, false, imageStream, "test.webp", "image/webp");
        var updated = await _albumService.GetAlbumByIdAsync(album.Id);

        // Assert
        updated!.Title.Should().Be(newTitle);
        updated.Year.Should().Be(newYear);
        updated.Description.Should().Be(newDescription);
        updated.ImageUrl.Should().Be(imageUrl);
        
        // Verify image was uploaded with normalized title (not ID)
        _mockImageStorageService.Verify(
            x => x.UploadImageAsync(It.IsAny<Stream>(), "test.webp", "image/webp", "albums", "updated_title"),
            Times.Once);
    }

    [Fact]
    public async Task UpdateAlbumWithCoverAsync_WithExistingImage_DeletesOldImage()
    {
        // Arrange
        var album = await _albumService.CreateAlbumAsync("Original Title", 2020);
        var oldImageUrl = "https://example.com/old-image.webp";
        var newImageUrl = "https://example.com/new-image.webp";
        
        // Set initial image
        album.SetCoverImage(oldImageUrl);
        _context.Albums.Update(album);
        await _context.SaveChangesAsync();
        
        _mockImageStorageService
            .Setup(x => x.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(newImageUrl);

        // Act
        using var imageStream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        await _albumService.UpdateAlbumWithCoverAsync(album.Id, "New Title", 2021, "New desc", false, imageStream, "test.webp", "image/webp");

        // Assert
        _mockImageStorageService.Verify(
            x => x.DeleteImageAsync(oldImageUrl),
            Times.Once,
            "Old image should be deleted");
    }

    [Fact]
    public async Task UpdateAlbumWithCoverAsync_WithInvalidId_ThrowsException()
    {
        // Arrange
        using var imageStream = new MemoryStream(new byte[] { 1, 2, 3, 4 });

        // Act & Assert
        var act = async () => await _albumService.UpdateAlbumWithCoverAsync(999, "Title", 2020, "Description", false, imageStream, "test.webp", "image/webp");
        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task CreateAlbumAsync_WithIsPrivateTrue_CreatesPrivateAlbum()
    {
        // Arrange
        var title = "Private Album";
        var year = 2020;
        var description = "Private description";
        var isPrivate = true;

        // Act
        var result = await _albumService.CreateAlbumAsync(title, year, description, null, isPrivate);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be(title);
        result.IsPrivate.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAlbumAsync_WithoutIsPrivate_CreatesPublicAlbum()
    {
        // Arrange
        var title = "Public Album";
        var year = 2020;

        // Act
        var result = await _albumService.CreateAlbumAsync(title, year);

        // Assert
        result.Should().NotBeNull();
        result.IsPrivate.Should().BeFalse();
    }

    [Fact]
    public async Task GetPublicAlbumsAsync_ReturnsOnlyPublicAlbums()
    {
        // Arrange
        await _albumService.CreateAlbumAsync("Public Album 1", 2020, null, null, false);
        await _albumService.CreateAlbumAsync("Private Album", 2021, null, null, true);
        await _albumService.CreateAlbumAsync("Public Album 2", 2022, null, null, false);

        // Act
        var result = await _albumService.GetPublicAlbumsAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(a => !a.IsPrivate);
        result.Should().Contain(a => a.Title == "Public Album 1");
        result.Should().Contain(a => a.Title == "Public Album 2");
        result.Should().NotContain(a => a.Title == "Private Album");
    }

    [Fact]
    public async Task UpdateAlbumAsync_UpdatesIsPrivate()
    {
        // Arrange
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020, null, null, false);
        album.IsPrivate.Should().BeFalse();

        // Act
        await _albumService.UpdateAlbumAsync(album.Id, "Updated Album", 2021, "Updated", true);
        var updated = await _albumService.GetAlbumByIdAsync(album.Id);

        // Assert
        updated!.IsPrivate.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAlbumWithCoverAsync_UpdatesIsPrivate()
    {
        // Arrange
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020, null, null, false);
        var imageUrl = "https://example.com/new-image.webp";
        
        _mockImageStorageService
            .Setup(x => x.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(imageUrl);

        // Act
        using var imageStream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        await _albumService.UpdateAlbumWithCoverAsync(album.Id, "Updated Album", 2021, "Updated", true, imageStream, "test.webp", "image/webp");
        var updated = await _albumService.GetAlbumByIdAsync(album.Id);

        // Assert
        updated!.IsPrivate.Should().BeTrue();
        updated.ImageUrl.Should().Be(imageUrl);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
