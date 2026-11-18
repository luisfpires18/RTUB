using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

// EventServiceTests: Videos And Queries tests.
public partial class EventServiceTests
{
    [Fact]
    public async Task RemoveEventImageAsync_DeletesTheImageFromStorage_AndClearsIt()
    {
        var eventEntity = await _eventService.CreateEventAsync("Test Event", DateTime.Now.AddDays(7), "Test Location", EventType.Festival,
            imageUrl: "https://example.com/old-image.webp");

        await _eventService.RemoveEventImageAsync(eventEntity.Id);

        (await _context.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventEntity.Id))!.ImageUrl.Should().BeNull();
        _mockImageStorageService.Verify(x => x.DeleteImageAsync("https://example.com/old-image.webp"), Times.Once);
    }

    [Fact]
    public async Task RemoveEventImageAsync_WithoutAnImage_TouchesNothing()
    {
        var eventEntity = await _eventService.CreateEventAsync("Test Event", DateTime.Now.AddDays(7), "Test Location", EventType.Festival);

        await _eventService.RemoveEventImageAsync(eventEntity.Id);

        _mockImageStorageService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetEventsByTypeAsync_WithMultipleTypes_ReturnsOnlyMatchingType()
    {
        // Arrange
        _context.Events.Add(Event.Create("Festival 1", DateTime.Now.AddDays(7), "Location", EventType.Festival));
        _context.Events.Add(Event.Create("Festival 2", DateTime.Now.AddDays(8), "Location", EventType.Festival));
        _context.Events.Add(Event.Create("Atuacao 1", DateTime.Now.AddDays(9), "Location", EventType.Atuacao));
        _context.Events.Add(Event.Create("Casamento 1", DateTime.Now.AddDays(10), "Location", EventType.Casamento));
        await _context.SaveChangesAsync();

        // Act
        var result = await _eventService.GetEventsByTypeAsync(EventType.Festival);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(e => e.Type == EventType.Festival);
    }

    [Fact]
    public async Task GetEventsByTypeAsync_WithNoMatchingType_ReturnsEmpty()
    {
        // Arrange
        _context.Events.Add(Event.Create("Festival 1", DateTime.Now.AddDays(7), "Location", EventType.Festival));
        await _context.SaveChangesAsync();

        // Act
        var result = await _eventService.GetEventsByTypeAsync(EventType.Atuacao);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateVideoTitleAsync_WithOwner_UpdatesTitle()
    {
        // Arrange
        var userId = "user-1";
        var video = EventVideo.CreateVideo(1, "https://example.com/video.mp4", "video/mp4", 1000, userId, "Original Title", 0);
        video.Id = 1;

        var mockEventVideoRepository = new Mock<IEventVideoRepository>();
        var mockEventVideoStorageService = new Mock<IEventVideoStorageService>();
        var mockPushNotificationFactory = new Mock<IPushNotificationFactory>();
        var mockPushNotificationService = new Mock<IPushNotificationService>();
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        var mockUserManager = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        mockEventVideoRepository
            .Setup(x => x.GetByIdAsync(1))
            .ReturnsAsync(video);

        var videoService = new EventService(
            _eventRepository,
            _mockImageStorageService.Object,
            _enrollmentRepository,
            mockEventVideoRepository.Object,
            mockEventVideoStorageService.Object,
            mockPushNotificationFactory.Object,
            mockPushNotificationService.Object,
            mockUserManager.Object,
            _fixture.CreateContextFactory());

        // Act
        await videoService.UpdateVideoTitleAsync(1, "New Title", userId, false);

        // Assert
        video.Title.Should().Be("New Title");
        mockEventVideoRepository.Verify(x => x.UpdateAsync(video), Times.Once);
    }

    [Fact]
    public async Task UpdateVideoTitleAsync_WithAdmin_UpdatesTitle()
    {
        // Arrange
        var userId = "user-1";
        var adminId = "admin-1";
        var video = EventVideo.CreateVideo(1, "https://example.com/video.mp4", "video/mp4", 1000, userId, "Original Title", 0);
        video.Id = 1;

        var mockEventVideoRepository = new Mock<IEventVideoRepository>();
        var mockEventVideoStorageService = new Mock<IEventVideoStorageService>();
        var mockPushNotificationFactory = new Mock<IPushNotificationFactory>();
        var mockPushNotificationService = new Mock<IPushNotificationService>();
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        var mockUserManager = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        mockEventVideoRepository
            .Setup(x => x.GetByIdAsync(1))
            .ReturnsAsync(video);

        var videoService = new EventService(
            _eventRepository,
            _mockImageStorageService.Object,
            _enrollmentRepository,
            mockEventVideoRepository.Object,
            mockEventVideoStorageService.Object,
            mockPushNotificationFactory.Object,
            mockPushNotificationService.Object,
            mockUserManager.Object,
            _fixture.CreateContextFactory());

        // Act
        await videoService.UpdateVideoTitleAsync(1, "New Title", adminId, true);

        // Assert
        video.Title.Should().Be("New Title");
        mockEventVideoRepository.Verify(x => x.UpdateAsync(video), Times.Once);
    }

    [Fact]
    public async Task UpdateVideoTitleAsync_WithNonOwner_ThrowsUnauthorizedException()
    {
        // Arrange
        var userId = "user-1";
        var otherUserId = "user-2";
        var video = EventVideo.CreateVideo(1, "https://example.com/video.mp4", "video/mp4", 1000, userId, "Original Title", 0);
        video.Id = 1;

        var mockEventVideoRepository = new Mock<IEventVideoRepository>();
        var mockEventVideoStorageService = new Mock<IEventVideoStorageService>();
        var mockPushNotificationFactory = new Mock<IPushNotificationFactory>();
        var mockPushNotificationService = new Mock<IPushNotificationService>();
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        var mockUserManager = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        mockEventVideoRepository
            .Setup(x => x.GetByIdAsync(1))
            .ReturnsAsync(video);

        var videoService = new EventService(
            _eventRepository,
            _mockImageStorageService.Object,
            _enrollmentRepository,
            mockEventVideoRepository.Object,
            mockEventVideoStorageService.Object,
            mockPushNotificationFactory.Object,
            mockPushNotificationService.Object,
            mockUserManager.Object,
            _fixture.CreateContextFactory());

        // Act & Assert
        var act = async () => await videoService.UpdateVideoTitleAsync(1, "New Title", otherUserId, false);
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*permission*");
    }

    [Fact]
    public async Task DeleteVideoAsync_WithOwner_DeletesVideo()
    {
        // Arrange
        var userId = "user-1";
        var videoUrl = "https://example.com/video.mp4";
        var video = EventVideo.CreateVideo(1, videoUrl, "video/mp4", 1000, userId, "Title", 0);
        video.Id = 1;

        var mockEventVideoRepository = new Mock<IEventVideoRepository>();
        var mockEventVideoStorageService = new Mock<IEventVideoStorageService>();
        var mockPushNotificationFactory = new Mock<IPushNotificationFactory>();
        var mockPushNotificationService = new Mock<IPushNotificationService>();
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        var mockUserManager = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        mockEventVideoRepository
            .Setup(x => x.GetByIdAsync(1))
            .ReturnsAsync(video);

        var videoService = new EventService(
            _eventRepository,
            _mockImageStorageService.Object,
            _enrollmentRepository,
            mockEventVideoRepository.Object,
            mockEventVideoStorageService.Object,
            mockPushNotificationFactory.Object,
            mockPushNotificationService.Object,
            mockUserManager.Object,
            _fixture.CreateContextFactory());

        // Act
        await videoService.DeleteVideoAsync(1, userId, false);

        // Assert
        mockEventVideoStorageService.Verify(x => x.DeleteVideoAsync(videoUrl), Times.Once);
        mockEventVideoRepository.Verify(x => x.DeleteAsync(video), Times.Once);
    }

    [Fact]
    public async Task DeleteVideoAsync_WithNonOwner_ThrowsUnauthorizedException()
    {
        // Arrange
        var userId = "user-1";
        var otherUserId = "user-2";
        var video = EventVideo.CreateVideo(1, "https://example.com/video.mp4", "video/mp4", 1000, userId, "Title", 0);
        video.Id = 1;

        var mockEventVideoRepository = new Mock<IEventVideoRepository>();
        var mockEventVideoStorageService = new Mock<IEventVideoStorageService>();
        var mockPushNotificationFactory = new Mock<IPushNotificationFactory>();
        var mockPushNotificationService = new Mock<IPushNotificationService>();
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        var mockUserManager = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        mockEventVideoRepository
            .Setup(x => x.GetByIdAsync(1))
            .ReturnsAsync(video);

        var videoService = new EventService(
            _eventRepository,
            _mockImageStorageService.Object,
            _enrollmentRepository,
            mockEventVideoRepository.Object,
            mockEventVideoStorageService.Object,
            mockPushNotificationFactory.Object,
            mockPushNotificationService.Object,
            mockUserManager.Object,
            _fixture.CreateContextFactory());

        // Act & Assert
        var act = async () => await videoService.DeleteVideoAsync(1, otherUserId, false);
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*permission*");
    }
}
