using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
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
/// Unit tests for EventService
/// Tests business logic and service layer operations with Repository pattern
/// </summary>
public class EventServiceTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly EventRepository _eventRepository;
    private readonly EnrollmentRepository _enrollmentRepository;
    private readonly EventService _eventService;
    private readonly Mock<IImageStorageService> _mockImageStorageService;

    public EventServiceTests(DatabaseFixture fixture)
    {
        // Clean database at constructor start to ensure test isolation
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();

        _fixture = fixture;
        _context = _fixture.CreateContext();
        _mockImageStorageService = new Mock<IImageStorageService>();
        _eventRepository = new EventRepository(_context);
        _enrollmentRepository = new EnrollmentRepository(_context);
        var mockEventVideoRepository = new Mock<IEventVideoRepository>();
        var mockEventVideoStorageService = new Mock<IEventVideoStorageService>();

        // Mock dependencies for EventService
        var mockPushNotificationFactory = new Mock<IPushNotificationFactory>();
        var mockPushNotificationService = new Mock<IPushNotificationService>();
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        var mockUserManager = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _eventService = new EventService(
            _eventRepository,
            _mockImageStorageService.Object,
            _enrollmentRepository,
            mockEventVideoRepository.Object,
            mockEventVideoStorageService.Object,
            mockPushNotificationFactory.Object,
            mockPushNotificationService.Object,
            mockUserManager.Object,
            mockHttpContextAccessor.Object,
            _context);
    }

    [Fact]
    public async Task CreateEventAsync_WithValidData_ReturnsEvent()
    {
        // Arrange
        var name = "Test Event";
        var date = DateTime.Now.AddDays(7);
        var location = "Test Location";
        var type = EventType.Festival;
        var description = "Test Description";

        // Act
        var result = await _eventService.CreateEventAsync(name, date, location, type, description);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be(name);
        result.Location.Should().Be(location);
        result.Type.Should().Be(type);
        result.Description.Should().Be(description);
    }

    [Fact]
    public async Task GetEventByIdAsync_ExistingEvent_ReturnsEvent()
    {
        // Arrange
        var eventEntity = Event.Create("Test Event", DateTime.Now, "Test Location", EventType.Festival);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        // Act
        var result = await _eventService.GetEventByIdAsync(eventEntity.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(eventEntity.Id);
        result.Name.Should().Be("Test Event");
    }

    [Fact]
    public async Task GetAllEventsAsync_WithMultipleEvents_ReturnsAllEvents()
    {
        // Arrange
        _context.Events.Add(Event.Create("Event 1", DateTime.Now, "Location 1", EventType.Festival));
        _context.Events.Add(Event.Create("Event 2", DateTime.Now.AddDays(1), "Location 2", EventType.Atuacao));
        await _context.SaveChangesAsync();

        // Act
        var result = await _eventService.GetAllEventsAsync();

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetUpcomingEventsAsync_WithTodayEvent_IncludesEvent()
    {
        // Arrange
        var today = DateTime.Today;
        _context.Events.Add(Event.Create("Today Event", today, "Location", EventType.Festival));
        _context.Events.Add(Event.Create("Future Event", today.AddDays(1), "Location", EventType.Festival));
        _context.Events.Add(Event.Create("Past Event", today.AddDays(-1), "Location", EventType.Festival));
        await _context.SaveChangesAsync();

        // Act
        var result = (await _eventService.GetUpcomingEventsAsync(10)).ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(e => e.Name == "Today Event");
        result.Should().Contain(e => e.Name == "Future Event");
        result.Should().NotContain(e => e.Name == "Past Event");
    }

    [Fact]
    public async Task GetPastEventsAsync_WithTodayEvent_ExcludesEvent()
    {
        // Arrange
        var today = DateTime.Today;
        _context.Events.Add(Event.Create("Today Event", today, "Location", EventType.Festival));
        _context.Events.Add(Event.Create("Future Event", today.AddDays(1), "Location", EventType.Festival));
        _context.Events.Add(Event.Create("Past Event", today.AddDays(-1), "Location", EventType.Festival));
        await _context.SaveChangesAsync();

        // Act
        var result = (await _eventService.GetPastEventsAsync(10)).ToList();

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain(e => e.Name == "Past Event");
        result.Should().NotContain(e => e.Name == "Today Event");
        result.Should().NotContain(e => e.Name == "Future Event");
    }

    [Fact]
    public async Task GetUpcomingEventsAsync_WithEventEndingToday_IncludesEvent()
    {
        // Arrange
        var today = DateTime.Today;
        var eventEntity = Event.Create("Multi-day Event", today.AddDays(-2), "Location", EventType.Festival);
        eventEntity.SetEndDate(today);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        // Act
        var result = (await _eventService.GetUpcomingEventsAsync(10)).ToList();

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain(e => e.Name == "Multi-day Event");
    }

    [Fact]
    public async Task GetPastEventsAsync_WithEventEndingYesterday_IncludesEvent()
    {
        // Arrange
        var today = DateTime.Today;
        var eventEntity = Event.Create("Multi-day Event", today.AddDays(-3), "Location", EventType.Festival);
        eventEntity.SetEndDate(today.AddDays(-1));
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        // Act
        var result = (await _eventService.GetPastEventsAsync(10)).ToList();

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain(e => e.Name == "Multi-day Event");
    }

    [Fact]
    public async Task CancelEventAsync_WithValidReason_CancelsEvent()
    {
        // Arrange
        var eventEntity = Event.Create("Test Event", DateTime.Now.AddDays(7), "Test Location", EventType.Atuacao);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();
        var eventId = eventEntity.Id;
        var cancellationReason = "Mau tempo previsto";

        // Act
        await _eventService.CancelEventAsync(eventId, cancellationReason);

        // Assert
        var cancelledEvent = await _context.Events.FindAsync(eventId);
        cancelledEvent.Should().NotBeNull();
        cancelledEvent!.IsCancelled.Should().BeTrue();
        cancelledEvent.CancellationReason.Should().Be(cancellationReason);
    }

    [Fact]
    public async Task CancelEventAsync_WithNonExistentEvent_ThrowsException()
    {
        // Arrange
        var nonExistentEventId = 9999;
        var cancellationReason = "Mau tempo previsto";

        // Act & Assert
        var act = async () => await _eventService.CancelEventAsync(nonExistentEventId, cancellationReason);
        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage($"Event with ID {nonExistentEventId} not found");
    }

    [Fact]
    public async Task UncancelEventAsync_WithCancelledEvent_UncancelsEvent()
    {
        // Arrange
        var eventEntity = Event.Create("Test Event", DateTime.Now.AddDays(7), "Test Location", EventType.Atuacao);
        eventEntity.Cancel("Mau tempo previsto");
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();
        var eventId = eventEntity.Id;

        // Act
        await _eventService.UncancelEventAsync(eventId);

        // Assert
        var uncancelledEvent = await _context.Events.FindAsync(eventId);
        uncancelledEvent.Should().NotBeNull();
        uncancelledEvent!.IsCancelled.Should().BeFalse();
        uncancelledEvent.CancellationReason.Should().BeNull();
    }

    [Fact]
    public async Task UncancelEventAsync_WithNonExistentEvent_ThrowsException()
    {
        // Arrange
        var nonExistentEventId = 9999;

        // Act & Assert
        var act = async () => await _eventService.UncancelEventAsync(nonExistentEventId);
        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage($"Event with ID {nonExistentEventId} not found");
    }

    [Fact]
    public async Task UpdateEventWithImageAsync_UpdatesEventDetailsAndImage()
    {
        // Arrange
        var eventEntity = await _eventService.CreateEventAsync("Original Name", DateTime.Now, "Original Location", EventType.Atuacao);
        var newName = "Updated Name";
        var newDate = DateTime.Now.AddDays(7);
        var newLocation = "Updated Location";
        var newDescription = "Updated description";
        var newEndDate = DateTime.Now.AddDays(8);
        var imageUrl = "https://example.com/test-image.webp";

        _mockImageStorageService
            .Setup(x => x.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(imageUrl);

        // Act
        using var imageStream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        await _eventService.UpdateEventWithImageAsync(eventEntity.Id, newName, newDate, newLocation, newDescription, EventType.Atuacao, newEndDate, imageStream, "test.webp", "image/webp");
        var updated = await _eventService.GetEventByIdAsync(eventEntity.Id);

        // Assert
        updated!.Name.Should().Be(newName);
        updated.Date.Should().Be(newDate);
        updated.Location.Should().Be(newLocation);
        updated.Description.Should().Be(newDescription);
        updated.EndDate.Should().Be(newEndDate);
        updated.ImageUrl.Should().Be(imageUrl);

        // Verify image was uploaded with normalized name (not ID)
        _mockImageStorageService.Verify(
            x => x.UploadImageAsync(It.IsAny<Stream>(), "test.webp", "image/webp", "events", "updated_name"),
            Times.Once);
    }

    [Fact]
    public async Task UpdateEventWithImageAsync_WithExistingImage_DeletesOldImage()
    {
        // Arrange
        var eventEntity = await _eventService.CreateEventAsync("Test Event", DateTime.Now, "Test Location", EventType.Festival);
        var oldImageUrl = "https://example.com/old-image.webp";
        var newImageUrl = "https://example.com/new-image.webp";

        // Set initial image
        eventEntity.SetImage(oldImageUrl);
        _context.Events.Update(eventEntity);
        await _context.SaveChangesAsync();

        _mockImageStorageService
            .Setup(x => x.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(newImageUrl);

        // Act
        using var imageStream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        await _eventService.UpdateEventWithImageAsync(eventEntity.Id, "New Name", DateTime.Now.AddDays(5), "New Location", "New desc", EventType.Festival, null, imageStream, "test.webp", "image/webp");

        // Assert
        _mockImageStorageService.Verify(
            x => x.DeleteImageAsync(oldImageUrl),
            Times.Once,
            "Old image should be deleted");
    }

    [Fact]
    public async Task UpdateEventWithImageAsync_WithInvalidId_ThrowsException()
    {
        // Arrange
        using var imageStream = new MemoryStream(new byte[] { 1, 2, 3, 4 });

        // Act & Assert
        var act = async () => await _eventService.UpdateEventWithImageAsync(999, "Name", DateTime.Now, "Location", "Description", EventType.Atuacao, null, imageStream, "test.webp", "image/webp");
        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task CreateEventAsync_WithEndDate_CreatesEventWithEndDate()
    {
        // Arrange
        var name = "Event with EndDate";
        var date = DateTime.Now.AddDays(5);
        var endDate = DateTime.Now.AddDays(7);
        var location = "Test Location";
        var type = EventType.Atuacao;

        // Act
        var evt = await _eventService.CreateEventAsync(name, date, location, type, "Description", endDate);

        // Assert
        evt.Should().NotBeNull();
        evt.Name.Should().Be(name);
        evt.Date.Should().Be(date);
        evt.EndDate.Should().Be(endDate);
    }

    [Fact]
    public async Task UpdateEventAsync_WithValidData_UpdatesEvent()
    {
        // Arrange
        var eventEntity = await _eventService.CreateEventAsync("Original Event", DateTime.Now.AddDays(5), "Original Location", EventType.Festival, "Original Description");
        var newName = "Updated Event";
        var newDate = DateTime.Now.AddDays(10);
        var newLocation = "Updated Location";
        var newDescription = "Updated Description";
        var newType = EventType.Atuacao;

        // Act
        await _eventService.UpdateEventAsync(eventEntity.Id, newName, newDate, newLocation, newDescription, newType);
        var updated = await _eventService.GetEventByIdAsync(eventEntity.Id);

        // Assert
        updated.Should().NotBeNull();
        updated!.Name.Should().Be(newName);
        updated.Date.Should().Be(newDate);
        updated.Location.Should().Be(newLocation);
        updated.Description.Should().Be(newDescription);
        updated.Type.Should().Be(newType);
    }

    [Fact]
    public async Task UpdateEventAsync_WithEnrollments_DoesNotThrowTrackingException()
    {
        // Arrange - This test reproduces the tracking issue when updating events with enrollments
        var user = new ApplicationUser
        {
            Id = "test-user-1",
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Nickname = "TestNick",
            PhoneNumber = "123456789"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var eventEntity = await _eventService.CreateEventAsync("Event with Enrollments", DateTime.Now.AddDays(7), "Test Location", EventType.Festival);

        var enrollment = Enrollment.Create(user.Id, eventEntity.Id);
        enrollment.WillAttend = true;
        enrollment.Instrument = InstrumentType.Guitarra;
        _context.Enrollments.Add(enrollment);
        await _context.SaveChangesAsync();

        // Load the event with enrollments (simulating typical usage)
        var loadedEvent = await _context.Events
            .Include(e => e.Enrollments)
            .ThenInclude(en => en.User)
            .FirstOrDefaultAsync(e => e.Id == eventEntity.Id);
        loadedEvent.Should().NotBeNull();

        // Act - Update the event (this should not throw tracking exception)
        var act = async () => await _eventService.UpdateEventAsync(eventEntity.Id, "Updated Name", DateTime.Now.AddDays(8), "Updated Location", "Updated Description", EventType.Atuacao);

        // Assert
        await act.Should().NotThrowAsync();

        var updated = await _eventService.GetEventByIdAsync(eventEntity.Id);
        updated.Should().NotBeNull();
        updated!.Name.Should().Be("Updated Name");
    }

    [Fact]
    public async Task DeleteEventAsync_WithExistingEvent_DeletesEvent()
    {
        // Arrange
        var eventEntity = await _eventService.CreateEventAsync("Event to Delete", DateTime.Now.AddDays(7), "Test Location", EventType.Festival);
        var eventId = eventEntity.Id;

        // Act
        await _eventService.DeleteEventAsync(eventId);

        // Assert
        var deleted = await _eventService.GetEventByIdAsync(eventId);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteEventAsync_WithImage_DeletesImageFromStorage()
    {
        // Arrange
        var eventEntity = await _eventService.CreateEventAsync("Event with Image", DateTime.Now.AddDays(7), "Test Location", EventType.Festival);
        var imageUrl = "https://example.com/test-image.webp";
        eventEntity.SetImage(imageUrl);
        _context.Events.Update(eventEntity);
        await _context.SaveChangesAsync();

        // Act
        await _eventService.DeleteEventAsync(eventEntity.Id);

        // Assert
        _mockImageStorageService.Verify(
            x => x.DeleteImageAsync(imageUrl),
            Times.Once,
            "Image should be deleted from storage when event is deleted");
    }

    [Fact]
    public async Task DeleteEventAsync_WithNonExistentEvent_ThrowsException()
    {
        // Act & Assert
        var act = async () => await _eventService.DeleteEventAsync(9999);
        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task SetEventImageAsync_WithExistingEvent_SetsImage()
    {
        // Arrange
        var eventEntity = await _eventService.CreateEventAsync("Test Event", DateTime.Now.AddDays(7), "Test Location", EventType.Festival);
        var imageUrl = "https://example.com/new-image.webp";

        _mockImageStorageService
            .Setup(x => x.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(imageUrl);

        // Act
        using var imageStream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        await _eventService.SetEventImageAsync(eventEntity.Id, imageStream, "test.webp", "image/webp");
        var updated = await _eventService.GetEventByIdAsync(eventEntity.Id);

        // Assert
        updated!.ImageUrl.Should().Be(imageUrl);
        _mockImageStorageService.Verify(
            x => x.UploadImageAsync(It.IsAny<Stream>(), "test.webp", "image/webp", "events", It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task SetEventImageAsync_WithExistingImage_DeletesOldImage()
    {
        // Arrange
        var eventEntity = await _eventService.CreateEventAsync("Test Event", DateTime.Now.AddDays(7), "Test Location", EventType.Festival);
        var oldImageUrl = "https://example.com/old-image.webp";
        var newImageUrl = "https://example.com/new-image.webp";
        eventEntity.SetImage(oldImageUrl);
        _context.Events.Update(eventEntity);
        await _context.SaveChangesAsync();

        _mockImageStorageService
            .Setup(x => x.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(newImageUrl);

        // Act
        using var imageStream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        await _eventService.SetEventImageAsync(eventEntity.Id, imageStream, "test.webp", "image/webp");

        // Assert
        _mockImageStorageService.Verify(
            x => x.DeleteImageAsync(oldImageUrl),
            Times.Once,
            "Old image should be deleted when setting new image");
    }

    [Fact]
    public async Task SetEventImageAsync_WithNonExistentEvent_ThrowsException()
    {
        // Arrange
        using var imageStream = new MemoryStream(new byte[] { 1, 2, 3, 4 });

        // Act & Assert
        var act = async () => await _eventService.SetEventImageAsync(9999, imageStream, "test.webp", "image/webp");
        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("*not found*");
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
    public async Task CreateEventAsync_WithImageUrl_SetsImageUrl()
    {
        // Arrange
        var name = "Event with Image";
        var date = DateTime.Now.AddDays(7);
        var location = "Test Location";
        var type = EventType.Festival;
        var imageUrl = "https://example.com/image.webp";

        // Act
        var result = await _eventService.CreateEventAsync(name, date, location, type, "Description", null, imageUrl);

        // Assert
        result.Should().NotBeNull();
        result.ImageUrl.Should().Be(imageUrl);
    }

    [Fact]
    public async Task UpdateEventAsync_WithEndDate_SetsEndDate()
    {
        // Arrange
        var eventEntity = await _eventService.CreateEventAsync("Test Event", DateTime.Now.AddDays(5), "Location", EventType.Festival);
        var newEndDate = DateTime.Now.AddDays(7);

        // Act
        await _eventService.UpdateEventAsync(eventEntity.Id, "Test Event", DateTime.Now.AddDays(5), "Location", "Description", EventType.Festival, newEndDate);
        var updated = await _eventService.GetEventByIdAsync(eventEntity.Id);

        // Assert
        updated.Should().NotBeNull();
        updated!.EndDate.Should().Be(newEndDate);
    }

    [Fact]
    public async Task UpdateEventAsync_WithNullEndDate_ClearsEndDate()
    {
        // Arrange
        var eventEntity = await _eventService.CreateEventAsync("Test Event", DateTime.Now.AddDays(5), "Location", EventType.Festival, "Description", DateTime.Now.AddDays(7));
        var updatedEvent = await _eventService.GetEventByIdAsync(eventEntity.Id);
        updatedEvent!.EndDate.Should().NotBeNull(); // Verify it was set

        // Act
        await _eventService.UpdateEventAsync(eventEntity.Id, "Test Event", DateTime.Now.AddDays(5), "Location", "Description", EventType.Festival, null);
        var updated = await _eventService.GetEventByIdAsync(eventEntity.Id);

        // Assert
        updated.Should().NotBeNull();
        updated!.EndDate.Should().BeNull();
    }

    [Fact]
    public async Task CancelEventAsync_DeletesAllEnrollments()
    {
        // Arrange
        var user1 = new ApplicationUser
        {
            Id = "user-1",
            UserName = "user1",
            Email = "user1@test.com",
            FirstName = "User",
            LastName = "One",
            Nickname = "User1"
        };
        var user2 = new ApplicationUser
        {
            Id = "user-2",
            UserName = "user2",
            Email = "user2@test.com",
            FirstName = "User",
            LastName = "Two",
            Nickname = "User2"
        };
        _context.Users.AddRange(user1, user2);
        await _context.SaveChangesAsync();

        var eventEntity = await _eventService.CreateEventAsync("Event to Cancel", DateTime.Now.AddDays(7), "Location", EventType.Festival);

        var enrollment1 = Enrollment.Create(user1.Id, eventEntity.Id);
        enrollment1.WillAttend = true;
        var enrollment2 = Enrollment.Create(user2.Id, eventEntity.Id);
        enrollment2.WillAttend = true;
        _context.Enrollments.AddRange(enrollment1, enrollment2);
        await _context.SaveChangesAsync();

        // Act
        await _eventService.CancelEventAsync(eventEntity.Id, "Cancelled");

        // Assert
        var enrollments = await _context.Enrollments.Where(e => e.EventId == eventEntity.Id).ToListAsync();
        enrollments.Should().BeEmpty();

        var cancelledEvent = await _eventService.GetEventByIdAsync(eventEntity.Id);
        cancelledEvent!.IsCancelled.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateEventAsync_WithNonExistentEvent_ThrowsException()
    {
        // Act & Assert
        var act = async () => await _eventService.UpdateEventAsync(9999, "Name", DateTime.Now, "Location", "Description", EventType.Festival);
        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task GetEventByIdAsync_WithNonExistentEvent_ReturnsNull()
    {
        // Act
        var result = await _eventService.GetEventByIdAsync(9999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateEventAsync_WithEmptyImageUrl_DoesNotSetImage()
    {
        // Arrange
        var name = "Event without Image";
        var date = DateTime.Now.AddDays(7);
        var location = "Test Location";
        var type = EventType.Festival;

        // Act
        var result = await _eventService.CreateEventAsync(name, date, location, type, "Description", null, "");

        // Assert
        result.Should().NotBeNull();
        result.ImageUrl.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task CreateEventAsync_WithNullImageUrl_DoesNotSetImage()
    {
        // Arrange
        var name = "Event without Image";
        var date = DateTime.Now.AddDays(7);
        var location = "Test Location";
        var type = EventType.Festival;

        // Act
        var result = await _eventService.CreateEventAsync(name, date, location, type, "Description", null, null);

        // Assert
        result.Should().NotBeNull();
        result.ImageUrl.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task GetVideosByEventIdAsync_WithExistingVideos_ReturnsVideos()
    {
        // Arrange
        var eventEntity = await _eventService.CreateEventAsync("Test Event", DateTime.Now.AddDays(7), "Location", EventType.Festival);
        var mockEventVideoRepository = new Mock<IEventVideoRepository>();
        var mockEventVideoStorageService = new Mock<IEventVideoStorageService>();
        var mockPushNotificationFactory = new Mock<IPushNotificationFactory>();
        var mockPushNotificationService = new Mock<IPushNotificationService>();
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        var mockUserManager = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var video1 = EventVideo.CreateVideo(eventEntity.Id, "https://example.com/video1.mp4", "video/mp4", 1000, "user-1", "Video 1", 0);
        video1.Id = 1;
        var video2 = EventVideo.CreateVideo(eventEntity.Id, "https://example.com/video2.mp4", "video/mp4", 2000, "user-2", "Video 2", 1);
        video2.Id = 2;

        mockEventVideoRepository
            .Setup(x => x.GetByEventIdAsync(eventEntity.Id))
            .ReturnsAsync(new[] { video1, video2 });

        var videoService = new EventService(
            _eventRepository,
            _mockImageStorageService.Object,
            _enrollmentRepository,
            mockEventVideoRepository.Object,
            mockEventVideoStorageService.Object,
            mockPushNotificationFactory.Object,
            mockPushNotificationService.Object,
            mockUserManager.Object,
            mockHttpContextAccessor.Object,
            _context);

        // Act
        var result = await videoService.GetVideosByEventIdAsync(eventEntity.Id);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(v => v.Id == 1);
        result.Should().Contain(v => v.Id == 2);
    }

    [Fact]
    public async Task GetVideosByEventIdAsync_WithNoVideos_ReturnsEmpty()
    {
        // Arrange
        var eventEntity = await _eventService.CreateEventAsync("Test Event", DateTime.Now.AddDays(7), "Location", EventType.Festival);
        var mockEventVideoRepository = new Mock<IEventVideoRepository>();
        var mockEventVideoStorageService = new Mock<IEventVideoStorageService>();
        var mockPushNotificationFactory = new Mock<IPushNotificationFactory>();
        var mockPushNotificationService = new Mock<IPushNotificationService>();
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        var mockUserManager = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        mockEventVideoRepository
            .Setup(x => x.GetByEventIdAsync(eventEntity.Id))
            .ReturnsAsync(Enumerable.Empty<EventVideo>());

        var videoService = new EventService(
            _eventRepository,
            _mockImageStorageService.Object,
            _enrollmentRepository,
            mockEventVideoRepository.Object,
            mockEventVideoStorageService.Object,
            mockPushNotificationFactory.Object,
            mockPushNotificationService.Object,
            mockUserManager.Object,
            mockHttpContextAccessor.Object,
            _context);

        // Act
        var result = await videoService.GetVideosByEventIdAsync(eventEntity.Id);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetVideoCountByEventIdAsync_ReturnsCorrectCount()
    {
        // Arrange
        var eventEntity = await _eventService.CreateEventAsync("Test Event", DateTime.Now.AddDays(7), "Location", EventType.Festival);
        var mockEventVideoRepository = new Mock<IEventVideoRepository>();
        var mockEventVideoStorageService = new Mock<IEventVideoStorageService>();
        var mockPushNotificationFactory = new Mock<IPushNotificationFactory>();
        var mockPushNotificationService = new Mock<IPushNotificationService>();
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        var mockUserManager = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        mockEventVideoRepository
            .Setup(x => x.GetCountByEventIdAsync(eventEntity.Id))
            .ReturnsAsync(5);

        var videoService = new EventService(
            _eventRepository,
            _mockImageStorageService.Object,
            _enrollmentRepository,
            mockEventVideoRepository.Object,
            mockEventVideoStorageService.Object,
            mockPushNotificationFactory.Object,
            mockPushNotificationService.Object,
            mockUserManager.Object,
            mockHttpContextAccessor.Object,
            _context);

        // Act
        var result = await videoService.GetVideoCountByEventIdAsync(eventEntity.Id);

        // Assert
        result.Should().Be(5);
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
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
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
            mockHttpContextAccessor.Object,
            _context);

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
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
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
            mockHttpContextAccessor.Object,
            _context);

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
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
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
            mockHttpContextAccessor.Object,
            _context);

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
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
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
            mockHttpContextAccessor.Object,
            _context);

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
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
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
            mockHttpContextAccessor.Object,
            _context);

        // Act & Assert
        var act = async () => await videoService.DeleteVideoAsync(1, otherUserId, false);
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*permission*");
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
