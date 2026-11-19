using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Repositories;
using RTUB.Application.Services;
using RTUB.Application.Utilities;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;

namespace RTUB.Integration.Tests;

/// <summary>
/// Integration tests for image storage functionality across services
/// Tests the full lifecycle of image uploads and deletions with R2 storage
/// </summary>
public class ImageStorageIntegrationTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IImageStorageService> _mockImageStorageService;
    private readonly EventService _eventService;

    public ImageStorageIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options, Mock.Of<Microsoft.AspNetCore.Http.IHttpContextAccessor>(), new AuditContext(), new RTUB.Application.Services.AuditLogAppender());
        _mockImageStorageService = new Mock<IImageStorageService>();

        var factory = WrapInFactory(options);

        // Mock dependencies for EventService
        var mockEventVideoRepository = new Mock<IEventVideoRepository>();
        var mockEventVideoStorageService = new Mock<IEventVideoStorageService>();
        var mockPushNotificationFactory = new Mock<IPushNotificationFactory>();
        var mockPushNotificationService = new Mock<IPushNotificationService>();
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        var mockUserManager = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _eventService = new EventService(
            new EventRepository(factory),
            _mockImageStorageService.Object,
            new EnrollmentRepository(factory),
            mockEventVideoRepository.Object,
            mockEventVideoStorageService.Object,
            mockPushNotificationFactory.Object,
            mockPushNotificationService.Object,
            mockUserManager.Object,
            factory);
    }

    #region Album Image Tests

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

        var normalizedName = S3KeyNormalizer.NormalizeForS3Key(eventEntity.Name);
        var imageUrl = "https://pub-test.r2.dev/rtub/images/event/1/event.jpg";

        _mockImageStorageService
            .Setup(x => x.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), "events", normalizedName))
            .ReturnsAsync(imageUrl);

        using var imageStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });

        // Act
        await _eventService.SetEventImageAsync(eventEntity.Id, imageStream, "event.jpg", "image/jpeg");

        // Assert
        _mockImageStorageService.Verify(
            x => x.UploadImageAsync(It.IsAny<Stream>(), "event.jpg", "image/jpeg", "events", normalizedName),
            Times.Once);

        var updatedEvent = await _context.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventEntity.Id);
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

        var normalizedName = S3KeyNormalizer.NormalizeForS3Key(eventEntity.Name);
        var imageUrl = "https://pub-test.r2.dev/rtub/images/event/1/event.jpg";

        _mockImageStorageService
            .Setup(x => x.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), "events", normalizedName))
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

        var deletedEvent = await _context.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventEntity.Id);
        deletedEvent.Should().BeNull();
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task EventService_SetEventImageAsync_WithInvalidId_ThrowsException()
    {
        // Arrange
        using var imageStream = new MemoryStream(new byte[] { 1, 2, 3 });

        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _eventService.SetEventImageAsync(999, imageStream, "event.jpg", "image/jpeg"));
    }

    #endregion

    public void Dispose()
    {
        _context.Dispose();
    }

    private static IDbContextFactory<ApplicationDbContext> WrapInFactory(DbContextOptions<ApplicationDbContext> options)
    {
        var mock = new Mock<IDbContextFactory<ApplicationDbContext>>();
        mock.Setup(f => f.CreateDbContext()).Returns(() => new ApplicationDbContext(options, Mock.Of<IHttpContextAccessor>(), new AuditContext(), new RTUB.Application.Services.AuditLogAppender()));
        return mock.Object;
    }
}
