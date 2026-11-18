// SongService: videos.
using FluentAssertions;
using Moq;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Tests.Services;

public partial class SongServiceTests
{
    #region Video Tests

    [Fact]
    public async Task AddVideoAsync_CreatesVideoWithGeneratedTitle()
    {
        // Arrange
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var fileName = "test-video.mp4";
        var contentType = "video/mp4";
        var userId = "user123";
        var title = "Test Video 1";

        var mockVideo = Core.Entities.SongVideo.CreateVideo(
            song.Id,
            "https://storage.com/video.mp4",
            contentType,
            stream.Length,
            userId,
            title
        );

        _mockSongVideoStorageService.Setup(s => s.UploadVideoAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), song.Id))
            .ReturnsAsync("https://storage.com/video.mp4");

        _mockSongVideoRepository.Setup(r => r.AddAsync(It.IsAny<Core.Entities.SongVideo>()))
            .ReturnsAsync(mockVideo);

        // Act
        var result = await _songService.AddVideoAsync(song.Id, stream, fileName, contentType, userId, title);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be(title);
        result.SongId.Should().Be(song.Id);
        result.CreatedByUserId.Should().Be(userId);
        _mockSongVideoStorageService.Verify(s => s.UploadVideoAsync(It.IsAny<Stream>(), fileName, contentType, song.Id), Times.Once);
    }

    [Fact]
    public async Task DeleteVideoAsync_WithValidUserPermissions_DeletesVideo()
    {
        // Arrange
        var videoId = 1;
        var userId = "user123";
        var mockVideo = Core.Entities.SongVideo.CreateVideo(
            1,
            "https://storage.com/video.mp4",
            "video/mp4",
            1024,
            userId,
            "Test Video"
        );

        _mockSongVideoRepository.Setup(r => r.GetByIdAsync(videoId))
            .ReturnsAsync(mockVideo);

        _mockSongVideoStorageService.Setup(s => s.DeleteVideoAsync(mockVideo.Url))
            .Returns(Task.CompletedTask);

        _mockSongVideoRepository.Setup(r => r.DeleteAsync(mockVideo))
            .Returns(Task.CompletedTask);

        // Act
        await _songService.DeleteVideoAsync(videoId, userId, isAdmin: false);

        // Assert
        _mockSongVideoStorageService.Verify(s => s.DeleteVideoAsync(mockVideo.Url), Times.Once);
        _mockSongVideoRepository.Verify(r => r.DeleteAsync(mockVideo), Times.Once);
    }

    [Fact]
    public async Task DeleteVideoAsync_WithAdminPermissions_DeletesVideoRegardlessOfOwner()
    {
        // Arrange
        var videoId = 1;
        var userId = "admin123";
        var mockVideo = Core.Entities.SongVideo.CreateVideo(
            1,
            "https://storage.com/video.mp4",
            "video/mp4",
            1024,
            "differentUser",  // Different user
            "Test Video"
        );

        _mockSongVideoRepository.Setup(r => r.GetByIdAsync(videoId))
            .ReturnsAsync(mockVideo);

        _mockSongVideoStorageService.Setup(s => s.DeleteVideoAsync(mockVideo.Url))
            .Returns(Task.CompletedTask);

        _mockSongVideoRepository.Setup(r => r.DeleteAsync(mockVideo))
            .Returns(Task.CompletedTask);

        // Act
        await _songService.DeleteVideoAsync(videoId, userId, isAdmin: true);

        // Assert
        _mockSongVideoStorageService.Verify(s => s.DeleteVideoAsync(mockVideo.Url), Times.Once);
        _mockSongVideoRepository.Verify(r => r.DeleteAsync(mockVideo), Times.Once);
    }

    [Fact]
    public async Task DeleteVideoAsync_WithoutPermissions_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var videoId = 1;
        var userId = "user123";
        var mockVideo = Core.Entities.SongVideo.CreateVideo(
            1,
            "https://storage.com/video.mp4",
            "video/mp4",
            1024,
            "differentUser",  // Different user
            "Test Video"
        );

        _mockSongVideoRepository.Setup(r => r.GetByIdAsync(videoId))
            .ReturnsAsync(mockVideo);

        // Act & Assert
        var act = async () => await _songService.DeleteVideoAsync(videoId, userId, isAdmin: false);
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*permission*");
    }

    [Fact]
    public async Task DeleteVideoAsync_WithNonExistentVideo_ThrowsEntityNotFoundException()
    {
        // Arrange
        var videoId = 999;
        var userId = "user123";

        _mockSongVideoRepository.Setup(r => r.GetByIdAsync(videoId))
            .ReturnsAsync((Core.Entities.SongVideo?)null);

        // Act & Assert
        var act = async () => await _songService.DeleteVideoAsync(videoId, userId, isAdmin: false);
        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("*SongVideo*");
    }

    #endregion
}
