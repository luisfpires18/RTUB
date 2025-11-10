using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using Xunit;

namespace RTUB.Application.Tests.Integration;

/// <summary>
/// Integration tests for image storage functionality across services
/// Tests the full lifecycle of image uploads and deletions with R2 storage
/// </summary>
public class ImageStorageIntegrationTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IImageStorageService> _mockImageStorageService;
    private readonly AlbumService _albumService;
    private readonly EventService _eventService;
    private readonly SlideshowService _slideshowService;

    public ImageStorageIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _mockImageStorageService = new Mock<IImageStorageService>();
        
        _albumService = new AlbumService(_context, _mockImageStorageService.Object);
        _eventService = new EventService(_context, _mockImageStorageService.Object);
        _slideshowService = new SlideshowService(_context, _mockImageStorageService.Object);
    }

    #region Album Image Tests

    [Fact]
    public async Task AlbumService_SetAlbumCoverAsync_UploadsToR2AndSavesUrl()
    {
        // Arrange
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var imageUrl = "https://pub-test.r2.dev/rtub/images/album/1/cover.jpg";
        
        _mockImageStorageService
            .Setup(x => x.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), "albums", album.Id.ToString()))
            .ReturnsAsync(imageUrl);

        using var imageStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });

        // Act
        await _albumService.SetAlbumCoverAsync(album.Id, imageStream, "cover.jpg", "image/jpeg");

        // Assert
        _mockImageStorageService.Verify(
            x => x.UploadImageAsync(It.IsAny<Stream>(), "cover.jpg", "image/jpeg", "albums", album.Id.ToString()),
            Times.Once);

        var updatedAlbum = await _albumService.GetAlbumByIdAsync(album.Id);
        updatedAlbum.Should().NotBeNull();
        updatedAlbum!.ImageUrl.Should().Be(imageUrl);
    }

    [Fact]
    public async Task AlbumService_SetAlbumCoverAsync_DeletesOldImageBeforeUploadingNew()
    {
        // Arrange
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var oldImageUrl = "https://pub-test.r2.dev/rtub/images/album/1/old-cover.jpg";
        var newImageUrl = "https://pub-test.r2.dev/rtub/images/album/1/new-cover.jpg";
        
        // Set initial cover
        _mockImageStorageService
            .Setup(x => x.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), "albums", album.Id.ToString()))
            .ReturnsAsync(oldImageUrl);
        
        using (var oldImageStream = new MemoryStream(new byte[] { 1, 2, 3 }))
        {
            await _albumService.SetAlbumCoverAsync(album.Id, oldImageStream, "old-cover.jpg", "image/jpeg");
        }

        // Setup for new upload
        _mockImageStorageService
            .Setup(x => x.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), "albums", album.Id.ToString()))
            .ReturnsAsync(newImageUrl);

        using var newImageStream = new MemoryStream(new byte[] { 4, 5, 6 });

        // Act
        await _albumService.SetAlbumCoverAsync(album.Id, newImageStream, "new-cover.jpg", "image/jpeg");

        // Assert - Old image should be deleted
        _mockImageStorageService.Verify(
            x => x.DeleteImageAsync(oldImageUrl),
            Times.Once);

        var updatedAlbum = await _albumService.GetAlbumByIdAsync(album.Id);
        updatedAlbum!.ImageUrl.Should().Be(newImageUrl);
    }

    [Fact]
    public async Task AlbumService_DeleteAlbumAsync_DeletesImageFromR2()
    {
        // Arrange
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var imageUrl = "https://pub-test.r2.dev/rtub/images/album/1/cover.jpg";
        
        _mockImageStorageService
            .Setup(x => x.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), "albums", album.Id.ToString()))
            .ReturnsAsync(imageUrl);

        using (var imageStream = new MemoryStream(new byte[] { 1, 2, 3 }))
        {
            await _albumService.SetAlbumCoverAsync(album.Id, imageStream, "cover.jpg", "image/jpeg");
        }

        // Act
        await _albumService.DeleteAlbumAsync(album.Id);

        // Assert
        _mockImageStorageService.Verify(
            x => x.DeleteImageAsync(imageUrl),
            Times.Once);

        var deletedAlbum = await _albumService.GetAlbumByIdAsync(album.Id);
        deletedAlbum.Should().BeNull();
    }

    #endregion

    #region Event Image Tests

    [Fact]
    public async Task EventService_SetEventImageAsync_UploadsToR2AndSavesUrl()
    {
        // Arrange
        var eventEntity = await _eventService.CreateEventAsync(
            "Test Event", 
            DateTime.Now.AddDays(1), 
            "Test Location", 
            Core.Enums.EventType.Festival,
            "Test Description");
        
        var imageUrl = "https://pub-test.r2.dev/rtub/images/event/1/event.jpg";
        
        _mockImageStorageService
            .Setup(x => x.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), "events", eventEntity.Id.ToString()))
            .ReturnsAsync(imageUrl);

        using var imageStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });

        // Act
        await _eventService.SetEventImageAsync(eventEntity.Id, imageStream, "event.jpg", "image/jpeg");

        // Assert
        _mockImageStorageService.Verify(
            x => x.UploadImageAsync(It.IsAny<Stream>(), "event.jpg", "image/jpeg", "events", eventEntity.Id.ToString()),
            Times.Once);

        var updatedEvent = await _eventService.GetEventByIdAsync(eventEntity.Id);
        updatedEvent.Should().NotBeNull();
        updatedEvent!.ImageUrl.Should().Be(imageUrl);
    }

    [Fact]
    public async Task EventService_DeleteEventAsync_DeletesImageFromR2()
    {
        // Arrange
        var eventEntity = await _eventService.CreateEventAsync(
            "Test Event", 
            DateTime.Now.AddDays(1), 
            "Test Location", 
            Core.Enums.EventType.Festival,
            "Test Description");
        
        var imageUrl = "https://pub-test.r2.dev/rtub/images/event/1/event.jpg";
        
        _mockImageStorageService
            .Setup(x => x.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), "events", eventEntity.Id.ToString()))
            .ReturnsAsync(imageUrl);

        using (var imageStream = new MemoryStream(new byte[] { 1, 2, 3 }))
        {
            await _eventService.SetEventImageAsync(eventEntity.Id, imageStream, "event.jpg", "image/jpeg");
        }

        // Act
        await _eventService.DeleteEventAsync(eventEntity.Id);

        // Assert
        _mockImageStorageService.Verify(
            x => x.DeleteImageAsync(imageUrl),
            Times.Once);

        var deletedEvent = await _eventService.GetEventByIdAsync(eventEntity.Id);
        deletedEvent.Should().BeNull();
    }

    #endregion

    #region Slideshow Image Tests

    [Fact]
    public async Task SlideshowService_SetSlideshowImageAsync_UploadsToR2AndSavesUrl()
    {
        // Arrange
        var slideshow = await _slideshowService.CreateSlideshowAsync("Test Slideshow", 1, "Test Description", 5000);
        var imageUrl = "https://pub-test.r2.dev/rtub/images/slideshow/1/slide.jpg";
        
        _mockImageStorageService
            .Setup(x => x.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), "slideshows", slideshow.Id.ToString()))
            .ReturnsAsync(imageUrl);

        using var imageStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });

        // Act
        await _slideshowService.SetSlideshowImageAsync(slideshow.Id, imageStream, "slide.jpg", "image/jpeg");

        // Assert
        _mockImageStorageService.Verify(
            x => x.UploadImageAsync(It.IsAny<Stream>(), "slide.jpg", "image/jpeg", "slideshows", slideshow.Id.ToString()),
            Times.Once);

        var updatedSlideshow = await _slideshowService.GetSlideshowByIdAsync(slideshow.Id);
        updatedSlideshow.Should().NotBeNull();
        updatedSlideshow!.ImageUrl.Should().Be(imageUrl);
    }

    [Fact]
    public async Task SlideshowService_DeleteSlideshowAsync_DeletesImageFromR2()
    {
        // Arrange
        var slideshow = await _slideshowService.CreateSlideshowAsync("Test Slideshow", 1, "Test Description", 5000);
        var imageUrl = "https://pub-test.r2.dev/rtub/images/slideshow/1/slide.jpg";
        
        _mockImageStorageService
            .Setup(x => x.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), "slideshows", slideshow.Id.ToString()))
            .ReturnsAsync(imageUrl);

        using (var imageStream = new MemoryStream(new byte[] { 1, 2, 3 }))
        {
            await _slideshowService.SetSlideshowImageAsync(slideshow.Id, imageStream, "slide.jpg", "image/jpeg");
        }

        // Act
        await _slideshowService.DeleteSlideshowAsync(slideshow.Id);

        // Assert
        _mockImageStorageService.Verify(
            x => x.DeleteImageAsync(imageUrl),
            Times.Once);

        var deletedSlideshow = await _slideshowService.GetSlideshowByIdAsync(slideshow.Id);
        deletedSlideshow.Should().BeNull();
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task AlbumService_SetAlbumCoverAsync_WithInvalidId_ThrowsException()
    {
        // Arrange
        using var imageStream = new MemoryStream(new byte[] { 1, 2, 3 });

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _albumService.SetAlbumCoverAsync(999, imageStream, "cover.jpg", "image/jpeg"));
    }

    [Fact]
    public async Task EventService_SetEventImageAsync_WithInvalidId_ThrowsException()
    {
        // Arrange
        using var imageStream = new MemoryStream(new byte[] { 1, 2, 3 });

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _eventService.SetEventImageAsync(999, imageStream, "event.jpg", "image/jpeg"));
    }

    [Fact]
    public async Task SlideshowService_SetSlideshowImageAsync_WithInvalidId_ThrowsException()
    {
        // Arrange
        using var imageStream = new MemoryStream(new byte[] { 1, 2, 3 });

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _slideshowService.SetSlideshowImageAsync(999, imageStream, "slide.jpg", "image/jpeg"));
    }

    #endregion

    public void Dispose()
    {
        _context.Dispose();
    }
}
