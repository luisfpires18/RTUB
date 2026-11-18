// NaipeService: content create.
using FluentAssertions;
using Moq;
using RTUB.Application.DTOs;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

public partial class NaipeServiceTests
{
    [Fact]
    public async Task CreateContentAsync_WithValidData_CreatesContent()
    {
        // Arrange
        var type = InstrumentType.Guitarra;
        var title = "Test Video";
        var description = "Test Description";
        var fileName = "test.mp4";
        var mimeType = "video/mp4";
        var isVideo = true;
        var sortOrder = 1.0m;
        var fileStream = new MemoryStream(new byte[] { 1, 2, 3, 4 });

        var expectedUrl = "https://example.com/videos/test.mp4";
        _mockMediaStorageService.Setup(m => m.UploadVideoAsync(It.IsAny<Stream>(), fileName, mimeType, type.ToString()))
            .ReturnsAsync(expectedUrl);

        // Act
        var result = await _naipeService.CreateContentAsync(
            type, title, description, fileStream, fileName, mimeType, isVideo, sortOrder, _testUser1.Id);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be(title);
        result.Description.Should().Be(description);
        result.InstrumentType.Should().Be(type);
        result.IsVideo.Should().BeTrue();
        result.Url.Should().Be(expectedUrl);
        result.CreatedByUserId.Should().Be(_testUser1.Id);

        var savedContent = await _context.NaipeContents.FindAsync(result.Id);
        savedContent.Should().NotBeNull();
        savedContent!.Title.Should().Be(title);
    }

    [Fact]
    public async Task CreateContentAsync_WithVideo_UploadsVideo()
    {
        // Arrange
        var type = InstrumentType.Baixo;
        var title = "Bass Tutorial";
        var fileName = "bass.mp4";
        var mimeType = "video/mp4";
        var fileStream = new MemoryStream(new byte[] { 1, 2, 3 });

        var expectedUrl = "https://example.com/videos/bass.mp4";
        _mockMediaStorageService.Setup(m => m.UploadVideoAsync(It.IsAny<Stream>(), fileName, mimeType, type.ToString()))
            .ReturnsAsync(expectedUrl);

        // Act
        var result = await _naipeService.CreateContentAsync(
            type, title, null, fileStream, fileName, mimeType, true, 1.0m, _testUser1.Id);

        // Assert
        result.Should().NotBeNull();
        result.IsVideo.Should().BeTrue();
        result.Url.Should().Be(expectedUrl);
        _mockMediaStorageService.Verify(m => m.UploadVideoAsync(It.IsAny<Stream>(), fileName, mimeType, type.ToString()), Times.Once);
        _mockMediaStorageService.Verify(m => m.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CreateContentAsync_WithImage_UploadsImage()
    {
        // Arrange
        var type = InstrumentType.Bandolim;
        var title = "Bandolim Image";
        var fileName = "bandolim.jpg";
        var mimeType = "image/jpeg";
        var fileStream = new MemoryStream(new byte[] { 1, 2, 3 });

        var expectedUrl = "https://example.com/images/bandolim.jpg";
        _mockMediaStorageService.Setup(m => m.UploadImageAsync(It.IsAny<Stream>(), fileName, mimeType, type.ToString()))
            .ReturnsAsync(expectedUrl);

        // Act
        var result = await _naipeService.CreateContentAsync(
            type, title, null, fileStream, fileName, mimeType, false, 1.0m, _testUser1.Id);

        // Assert
        result.Should().NotBeNull();
        result.IsVideo.Should().BeFalse();
        result.Url.Should().Be(expectedUrl);
        _mockMediaStorageService.Verify(m => m.UploadImageAsync(It.IsAny<Stream>(), fileName, mimeType, type.ToString()), Times.Once);
        _mockMediaStorageService.Verify(m => m.UploadVideoAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CreateContentAsync_SendsPushNotification()
    {
        // Arrange
        var type = InstrumentType.Guitarra;
        var title = "New Tutorial";
        var fileName = "tutorial.mp4";
        var mimeType = "video/mp4";
        var fileStream = new MemoryStream(new byte[] { 1, 2, 3 });

        var expectedUrl = "https://example.com/videos/tutorial.mp4";
        _mockMediaStorageService.Setup(m => m.UploadVideoAsync(It.IsAny<Stream>(), fileName, mimeType, type.ToString()))
            .ReturnsAsync(expectedUrl);

        // Setup push notification factory to return a notification DTO
        var notificationDto = new SendPushNotificationDto
        {
            Title = "Test Notification",
            Body = "Test Body"
        };
        _mockPushNotificationFactory.Setup(f => f.CreateNaipeContentNotification(
            title, type, true))
            .Returns(notificationDto);

        // Act
        await _naipeService.CreateContentAsync(
            type, title, null, fileStream, fileName, mimeType, true, 1.0m, _testUser1.Id);

        // Assert
        _mockPushNotificationFactory.Verify(f => f.CreateNaipeContentNotification(
            title, type, true), Times.Once);
        _mockPushNotificationService.Verify(s => s.BroadcastAsync(It.IsAny<SendPushNotificationDto>()), Times.Once);
    }
}
