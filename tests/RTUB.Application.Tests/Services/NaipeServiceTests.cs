using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Application.Repositories;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for NaipeService
/// Tests business logic and service layer operations with Repository pattern
/// </summary>
public class NaipeServiceTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly NaipeContentRepository _naipeContentRepository;
    private readonly NaipeCommentRepository _naipeCommentRepository;
    private readonly NaipeTypeConfigRepository _naipeTypeConfigRepository;
    private readonly NaipeService _naipeService;
    private readonly Mock<INaipeMediaStorageService> _mockMediaStorageService;
    private readonly Mock<IPushNotificationService> _mockPushNotificationService;
    private readonly Mock<IPushNotificationFactory> _mockPushNotificationFactory;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private ApplicationUser _testUser1;
    private ApplicationUser _testUser2;
    private ApplicationUser _adminUser;

    public NaipeServiceTests(DatabaseFixture fixture)
    {
        // Clean database at constructor start to ensure test isolation
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();

        _context = _fixture.CreateContext();
        _naipeContentRepository = new NaipeContentRepository(_fixture.CreateContextFactory());
        _naipeCommentRepository = new NaipeCommentRepository(_fixture.CreateContextFactory());
        _naipeTypeConfigRepository = new NaipeTypeConfigRepository(_fixture.CreateContextFactory());

        // Setup mocks
        _mockMediaStorageService = new Mock<INaipeMediaStorageService>();
        _mockPushNotificationService = new Mock<IPushNotificationService>();
        _mockPushNotificationFactory = new Mock<IPushNotificationFactory>();
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

        // Setup UserManager mock
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        _mockUserManager = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var auditContext = new AuditContext();
        var auditLogAppender = new AuditLogAppender();

        var cache = new MemoryCache(new MemoryCacheOptions());

        _naipeService = new NaipeService(
            _naipeContentRepository,
            _naipeCommentRepository,
            _naipeTypeConfigRepository,
            _mockMediaStorageService.Object,
            _mockPushNotificationService.Object,
            _mockPushNotificationFactory.Object,
            _mockUserManager.Object,
            _fixture.CreateContextFactory(),
            auditContext,
            _mockHttpContextAccessor.Object,
            cache);

        // Create test users (only if they don't exist for shared database)
        var userId1 = "naipe-test-user-1";
        var userId2 = "naipe-test-user-2";
        var adminUserId = "naipe-admin-user";

        if (!_context.Users.Any(u => u.Id == userId1))
        {
            _testUser1 = new ApplicationUser
            {
                Id = userId1,
                UserName = "naipe_testuser1",
                Email = "naipe_test1@test.com",
                FirstName = "Naipe",
                LastName = "User1",
                Nickname = "NaipeTestUser1"
            };
            _context.Users.Add(_testUser1);
        }
        else
        {
            _testUser1 = _context.Users.Find(userId1)!;
        }

        if (!_context.Users.Any(u => u.Id == userId2))
        {
            _testUser2 = new ApplicationUser
            {
                Id = userId2,
                UserName = "naipe_testuser2",
                Email = "naipe_test2@test.com",
                FirstName = "Naipe",
                LastName = "User2",
                Nickname = "NaipeTestUser2"
            };
            _context.Users.Add(_testUser2);
        }
        else
        {
            _testUser2 = _context.Users.Find(userId2)!;
        }

        if (!_context.Users.Any(u => u.Id == adminUserId))
        {
            _adminUser = new ApplicationUser
            {
                Id = adminUserId,
                UserName = "naipe_admin",
                Email = "naipe_admin@test.com",
                FirstName = "Naipe",
                LastName = "Admin",
                Nickname = "NaipeAdmin"
            };
            _context.Users.Add(_adminUser);
        }
        else
        {
            _adminUser = _context.Users.Find(adminUserId)!;
        }

        _context.SaveChanges();

        // Setup UserManager to return users and roles
        _mockUserManager.Setup(m => m.FindByIdAsync(_testUser1.Id))
            .ReturnsAsync(_testUser1);
        _mockUserManager.Setup(m => m.FindByIdAsync(_testUser2.Id))
            .ReturnsAsync(_testUser2);
        _mockUserManager.Setup(m => m.FindByIdAsync(_adminUser.Id))
            .ReturnsAsync(_adminUser);
        _mockUserManager.Setup(m => m.GetRolesAsync(_adminUser))
            .ReturnsAsync(new List<string> { "Admin" });
        _mockUserManager.Setup(m => m.GetRolesAsync(_testUser1))
            .ReturnsAsync(new List<string>());
        _mockUserManager.Setup(m => m.GetRolesAsync(_testUser2))
            .ReturnsAsync(new List<string>());
    }

    public void Dispose()
    {
        _context.Dispose();
    }

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
            title, It.IsAny<string>(), true, It.IsAny<string>()))
            .Returns(notificationDto);

        // Act
        await _naipeService.CreateContentAsync(
            type, title, null, fileStream, fileName, mimeType, true, 1.0m, _testUser1.Id);

        // Assert
        _mockPushNotificationFactory.Verify(f => f.CreateNaipeContentNotification(
            title, It.IsAny<string>(), true, It.IsAny<string>()), Times.Once);
        _mockPushNotificationService.Verify(s => s.BroadcastAsync(It.IsAny<SendPushNotificationDto>()), Times.Once);
    }

    [Fact]
    public async Task UpdateContentAsync_AsOwner_UpdatesContent()
    {
        // Arrange
        var content = NaipeContent.Create(
            InstrumentType.Guitarra, "Original Title", "https://example.com/video.mp4",
            "video/mp4", true, 1.0m, _testUser1.Id);
        _context.NaipeContents.Add(content);
        await _context.SaveChangesAsync();

        var newTitle = "Updated Title";
        var newDescription = "Updated Description";
        var newSortOrder = 2.0m;

        // Act
        await _naipeService.UpdateContentAsync(content.Id, newTitle, newDescription, newSortOrder, _testUser1.Id, false);

        // Assert
        var updatedContent = await _context.NaipeContents.FindAsync(content.Id);
        updatedContent.Should().NotBeNull();
        updatedContent!.Title.Should().Be(newTitle);
        updatedContent.Description.Should().Be(newDescription);
        updatedContent.SortOrder.Should().Be(newSortOrder);
    }

    [Fact]
    public async Task UpdateContentAsync_AsAdmin_UpdatesContent()
    {
        // Arrange
        var content = NaipeContent.Create(
            InstrumentType.Baixo, "Original Title", "https://example.com/video.mp4",
            "video/mp4", true, 1.0m, _testUser1.Id);
        _context.NaipeContents.Add(content);
        await _context.SaveChangesAsync();

        var newTitle = "Admin Updated Title";

        // Act
        await _naipeService.UpdateContentAsync(content.Id, newTitle, null, 1.0m, _adminUser.Id, true);

        // Assert
        var updatedContent = await _context.NaipeContents.FindAsync(content.Id);
        updatedContent.Should().NotBeNull();
        updatedContent!.Title.Should().Be(newTitle);
    }

    [Fact]
    public async Task UpdateContentAsync_AsOtherUser_ThrowsUnauthorizedException()
    {
        // Arrange
        var content = NaipeContent.Create(
            InstrumentType.Bandolim, "Original Title", "https://example.com/video.mp4",
            "video/mp4", true, 1.0m, _testUser1.Id);
        _context.NaipeContents.Add(content);
        await _context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await _naipeService.UpdateContentAsync(content.Id, "Hacked Title", null, 1.0m, _testUser2.Id, false));
    }

    [Fact]
    public async Task DeleteContentAsync_AsOwner_DeletesContent()
    {
        // Arrange
        var content = NaipeContent.Create(
            InstrumentType.Guitarra, "To Delete", "https://example.com/video.mp4",
            "video/mp4", true, 1.0m, _testUser1.Id);
        _context.NaipeContents.Add(content);
        await _context.SaveChangesAsync();

        var mediaUrl = content.Url;
        _mockMediaStorageService.Setup(m => m.DeleteMediaAsync(mediaUrl))
            .Returns(Task.CompletedTask);

        // Act
        await _naipeService.DeleteContentAsync(content.Id, _testUser1.Id, false);

        // Assert
        var deletedContent = await _context.NaipeContents.FindAsync(content.Id);
        deletedContent.Should().BeNull();
        _mockMediaStorageService.Verify(m => m.DeleteMediaAsync(mediaUrl), Times.Once);
    }

    [Fact]
    public async Task DeleteContentAsync_AsAdmin_DeletesContent()
    {
        // Arrange
        var content = NaipeContent.Create(
            InstrumentType.Baixo, "To Delete", "https://example.com/video.mp4",
            "video/mp4", true, 1.0m, _testUser1.Id);
        _context.NaipeContents.Add(content);
        await _context.SaveChangesAsync();

        var mediaUrl = content.Url;
        _mockMediaStorageService.Setup(m => m.DeleteMediaAsync(mediaUrl))
            .Returns(Task.CompletedTask);

        // Act
        await _naipeService.DeleteContentAsync(content.Id, _adminUser.Id, true);

        // Assert
        var deletedContent = await _context.NaipeContents.FindAsync(content.Id);
        deletedContent.Should().BeNull();
        _mockMediaStorageService.Verify(m => m.DeleteMediaAsync(mediaUrl), Times.Once);
    }

    [Fact]
    public async Task DeleteContentAsync_AsOtherUser_ThrowsUnauthorizedException()
    {
        // Arrange
        var content = NaipeContent.Create(
            InstrumentType.Bandolim, "To Delete", "https://example.com/video.mp4",
            "video/mp4", true, 1.0m, _testUser1.Id);
        _context.NaipeContents.Add(content);
        await _context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await _naipeService.DeleteContentAsync(content.Id, _testUser2.Id, false));

        // Verify content was NOT deleted
        var contentStillExists = await _context.NaipeContents.FindAsync(content.Id);
        contentStillExists.Should().NotBeNull();
        _mockMediaStorageService.Verify(m => m.DeleteMediaAsync(It.IsAny<string>()), Times.Never);
    }
}
