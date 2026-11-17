using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Tests.Services;

public class UserProfileServiceTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<IImageStorageService> _mockImageStorageService;
    private readonly Mock<ILeaderboardCommentRepository> _mockLeaderboardCommentRepository;
    private readonly Mock<ICommentRepository> _mockCommentRepository;
    private readonly Mock<IPostRepository> _mockPostRepository;
    private readonly Mock<IMeetingRepository> _mockMeetingRepository;
    private readonly Mock<IMeetingRequestRepository> _mockMeetingRequestRepository;
    private readonly Mock<ILogger<UserProfileService>> _mockLogger;
    private readonly UserProfileService _service;

    public UserProfileServiceTests(DatabaseFixture fixture)
    {
        // Clean database at constructor start to ensure test isolation
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();

        _context = _fixture.CreateContext();

        // Mock UserManager
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        _mockUserManager = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _mockImageStorageService = new Mock<IImageStorageService>();
        _mockLeaderboardCommentRepository = new Mock<ILeaderboardCommentRepository>();
        _mockCommentRepository = new Mock<ICommentRepository>();
        _mockPostRepository = new Mock<IPostRepository>();
        _mockMeetingRepository = new Mock<IMeetingRepository>();
        _mockMeetingRequestRepository = new Mock<IMeetingRequestRepository>();
        _mockLogger = new Mock<ILogger<UserProfileService>>();

        _service = new UserProfileService(
            _mockUserManager.Object,
            _fixture.CreateContextFactory(),
            _mockImageStorageService.Object,
            _mockLeaderboardCommentRepository.Object,
            _mockCommentRepository.Object,
            _mockPostRepository.Object,
            _mockMeetingRepository.Object,
            _mockMeetingRequestRepository.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task GetUserByIdAsync_WithValidId_ReturnsUser()
    {
        // Arrange
        var userId = "user-123";
        var user = new ApplicationUser { Id = userId, UserName = "testuser", Nickname = "TestUser" };
        _mockUserManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync(user);

        // Act
        var result = await _service.GetUserByIdAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(userId);
        result.UserName.Should().Be("testuser");
    }

    [Fact]
    public async Task GetUserByIdAsync_WithInvalidId_ReturnsNull()
    {
        // Arrange
        _mockUserManager.Setup(x => x.FindByIdAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);

        // Act
        var result = await _service.GetUserByIdAsync("invalid-id");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateProfilePictureAsync_WithValidFile_UpdatesPicture()
    {
        // Arrange
        var userId = "user-123";
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "testuser",
            Nickname = "TestUser",
            ImageUrl = "old-image-url.jpg"
        };
        var newImageUrl = "new-image-url.jpg";
        var imageStream = new MemoryStream(new byte[] { 1, 2, 3 });
        var fileName = "profile.jpg";
        var contentType = "image/jpeg";

        _mockUserManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync(user);
        _mockImageStorageService.Setup(x => x.DeleteImageAsync("old-image-url.jpg"))
            .Returns(Task.CompletedTask);
        _mockImageStorageService.Setup(x => x.UploadImageAsync(
            It.IsAny<Stream>(), fileName, contentType, "profile", "testuser"))
            .ReturnsAsync(newImageUrl);
        _mockUserManager.Setup(x => x.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        await _service.UpdateProfilePictureAsync(userId, imageStream, fileName, contentType);

        // Assert
        user.ImageUrl.Should().Be(newImageUrl);
        _mockImageStorageService.Verify(x => x.DeleteImageAsync("old-image-url.jpg"), Times.Once);
        _mockImageStorageService.Verify(x => x.UploadImageAsync(
            It.IsAny<Stream>(), fileName, contentType, "profile", "testuser"), Times.Once);
        _mockUserManager.Verify(x => x.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task UpdateProfilePictureAsync_WithInvalidUser_ThrowsException()
    {
        // Arrange
        var userId = "invalid-user";
        var imageStream = new MemoryStream(new byte[] { 1, 2, 3 });
        var fileName = "profile.jpg";
        var contentType = "image/jpeg";

        _mockUserManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync((ApplicationUser?)null);

        // Act
        var act = async () => await _service.UpdateProfilePictureAsync(userId, imageStream, fileName, contentType);

        // Assert
        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("ApplicationUser with ID invalid-user not found");
        _mockImageStorageService.Verify(x => x.UploadImageAsync(
            It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateProfilePictureAsync_WhenUpdateFails_ThrowsException()
    {
        // Arrange
        var userId = "user-123";
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "testuser",
            Nickname = "TestUser"
        };
        var newImageUrl = "new-image-url.jpg";
        var imageStream = new MemoryStream(new byte[] { 1, 2, 3 });
        var fileName = "profile.jpg";
        var contentType = "image/jpeg";

        _mockUserManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync(user);
        _mockImageStorageService.Setup(x => x.UploadImageAsync(
            It.IsAny<Stream>(), fileName, contentType, "profile", "testuser"))
            .ReturnsAsync(newImageUrl);
        _mockUserManager.Setup(x => x.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Update failed" }));

        // Act
        var act = async () => await _service.UpdateProfilePictureAsync(userId, imageStream, fileName, contentType);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Failed to update profile picture");
        // Image should still be uploaded even if update fails
        _mockImageStorageService.Verify(x => x.UploadImageAsync(
            It.IsAny<Stream>(), fileName, contentType, "profile", "testuser"), Times.Once);
    }

    [Fact]
    public async Task UpdateProfilePictureAsync_WithoutExistingImage_DoesNotDeleteOldImage()
    {
        // Arrange
        var userId = "user-123";
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "testuser",
            Nickname = "TestUser",
            ImageUrl = null // No existing image
        };
        var newImageUrl = "new-image-url.jpg";
        var imageStream = new MemoryStream(new byte[] { 1, 2, 3 });
        var fileName = "profile.jpg";
        var contentType = "image/jpeg";

        _mockUserManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync(user);
        _mockImageStorageService.Setup(x => x.UploadImageAsync(
            It.IsAny<Stream>(), fileName, contentType, "profile", "testuser"))
            .ReturnsAsync(newImageUrl);
        _mockUserManager.Setup(x => x.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        await _service.UpdateProfilePictureAsync(userId, imageStream, fileName, contentType);

        // Assert
        user.ImageUrl.Should().Be(newImageUrl);
        _mockImageStorageService.Verify(x => x.DeleteImageAsync(It.IsAny<string>()), Times.Never);
        _mockImageStorageService.Verify(x => x.UploadImageAsync(
            It.IsAny<Stream>(), fileName, contentType, "profile", "testuser"), Times.Once);
    }

    public void Dispose()
    {
        _context?.Dispose();
        GC.SuppressFinalize(this);
    }
}
