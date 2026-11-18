using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
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
public partial class EventServiceTests : IClassFixture<DatabaseFixture>, IDisposable
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
        _eventRepository = new EventRepository(_fixture.CreateContextFactory());
        _enrollmentRepository = new EnrollmentRepository(_fixture.CreateContextFactory());
        var mockEventVideoRepository = new Mock<IEventVideoRepository>();
        var mockEventVideoStorageService = new Mock<IEventVideoStorageService>();

        // Mock dependencies for EventService
        var mockPushNotificationFactory = new Mock<IPushNotificationFactory>();
        var mockPushNotificationService = new Mock<IPushNotificationService>();
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
            _fixture.CreateContextFactory());
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
        _context.ChangeTracker.Clear();
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
        _context.ChangeTracker.Clear();
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
        _context.ChangeTracker.Clear();
        var enrollments = await _context.Enrollments.Where(e => e.EventId == eventEntity.Id).ToListAsync();
        enrollments.Should().BeEmpty();

        var cancelledEvent = await _context.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventEntity.Id);
        cancelledEvent!.IsCancelled.Should().BeTrue();
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

    public void Dispose()
    {
        _context?.Dispose();
    }
}
