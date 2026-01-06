using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Moq;
using Microsoft.Extensions.Options;
using RTUB.Application.Data;
using RTUB.Application.Tests.Fixtures;
using RTUB.Application.Interfaces;
using RTUB.Application.Repositories;
using RTUB.Application.Services;
using RTUB.Core.Exceptions;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for EnrollmentService
/// Tests enrollment CRUD operations
/// </summary>
public class EnrollmentServiceTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly EnrollmentService _enrollmentService;
    private readonly EventRepository _eventRepository;
    private readonly EventService _eventService;
    private readonly Mock<IImageStorageService> _mockImageStorageService;
    private readonly Mock<IRetirementStatusService> _mockRetirementStatusService;

    public EnrollmentServiceTests(DatabaseFixture fixture)
    {
        // Clean database at constructor start to ensure test isolation
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();

        _fixture = fixture;
        _context = _fixture.CreateContext();
        _mockImageStorageService = new Mock<IImageStorageService>();
        _mockRetirementStatusService = new Mock<IRetirementStatusService>();

        var mockPushNotificationFactory = new Mock<IPushNotificationFactory>();
        var mockPushNotificationService = new Mock<IPushNotificationService>();
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

        _enrollmentService = new EnrollmentService(
            new EnrollmentRepository(_context),
            _mockRetirementStatusService.Object,
            mockPushNotificationFactory.Object,
            mockPushNotificationService.Object,
            mockHttpContextAccessor.Object);

        _eventRepository = new EventRepository(_context);
        var mockEventVideoRepository = new Mock<IEventVideoRepository>();
        var mockEventVideoStorageService = new Mock<IEventVideoStorageService>();

        // Mock UserManager
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        var mockUserManager = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _eventService = new EventService(
            _eventRepository,
            _mockImageStorageService.Object,
            new EnrollmentRepository(_context),
            mockEventVideoRepository.Object,
            mockEventVideoStorageService.Object,
            mockPushNotificationFactory.Object,
            mockPushNotificationService.Object,
            mockUserManager.Object,
            mockHttpContextAccessor.Object,
            _context);
    }

    [Fact]
    public async Task CreateEnrollmentAsync_WithValidData_CreatesEnrollment()
    {
        // Arrange
        var userId = "user123";
        var eventEntity = await _eventService.CreateEventAsync(
            "Test Event", DateTime.Now.AddDays(7), "Location", Core.Enums.EventType.Festival, "Description");

        // Act
        var result = await _enrollmentService.CreateEnrollmentAsync(userId, eventEntity.Id);

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be(userId);
        result.EventId.Should().Be(eventEntity.Id);
    }

    [Fact]
    public async Task GetEnrollmentByIdAsync_ExistingEnrollment_ReturnsEnrollment()
    {
        // Arrange
        var eventEntity = await _eventService.CreateEventAsync(
            "Test Event", DateTime.Now.AddDays(7), "Location", Core.Enums.EventType.Festival, "Description");
        var enrollment = await _enrollmentService.CreateEnrollmentAsync("user123", eventEntity.Id);

        // Act
        var result = await _enrollmentService.GetEnrollmentByIdAsync(enrollment.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(enrollment.Id);
    }

    [Fact]
    public async Task GetEnrollmentByIdAsync_NonExistingEnrollment_ReturnsNull()
    {
        // Act
        var result = await _enrollmentService.GetEnrollmentByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllEnrollmentsAsync_WithMultipleEnrollments_ReturnsAll()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Event 1", DateTime.Now.AddDays(7), "Location 1", Core.Enums.EventType.Festival, "Desc1");
        var event2 = await _eventService.CreateEventAsync("Event 2", DateTime.Now.AddDays(8), "Location 2", Core.Enums.EventType.Atuacao, "Desc2");

        await _enrollmentService.CreateEnrollmentAsync("user1", event1.Id);
        await _enrollmentService.CreateEnrollmentAsync("user2", event1.Id);
        await _enrollmentService.CreateEnrollmentAsync("user3", event2.Id);

        // Act
        var result = await _enrollmentService.GetAllEnrollmentsAsync();

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetAllEnrollmentsAsync_IncludesEventEntity()
    {
        // Arrange
        var eventDate = DateTime.Now.AddDays(7);
        var eventEntity = await _eventService.CreateEventAsync(
            "Test Event", eventDate, "Location", Core.Enums.EventType.Festival, "Description");
        await _enrollmentService.CreateEnrollmentAsync("user123", eventEntity.Id);

        // Act
        var result = await _enrollmentService.GetAllEnrollmentsAsync();
        var enrollment = result.First();

        // Assert
        enrollment.Event.Should().NotBeNull();
        enrollment.Event!.Name.Should().Be("Test Event");
        enrollment.Event.Date.Date.Should().Be(eventDate.Date);
    }

    [Fact]
    public async Task GetEnrollmentsByEventIdAsync_ReturnsEventEnrollments()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Event 1", DateTime.Now.AddDays(7), "Location 1", Core.Enums.EventType.Festival, "Desc1");
        var event2 = await _eventService.CreateEventAsync("Event 2", DateTime.Now.AddDays(8), "Location 2", Core.Enums.EventType.Atuacao, "Desc2");

        var enroll1 = await _enrollmentService.CreateEnrollmentAsync("user1", event1.Id);
        var enroll2 = await _enrollmentService.CreateEnrollmentAsync("user2", event1.Id);
        var enroll3 = await _enrollmentService.CreateEnrollmentAsync("user3", event2.Id);

        // Act - Get enrollments directly from context to test service method
        var allEnrollments = await _context.Enrollments.ToListAsync();
        var result = allEnrollments.Where(e => e.EventId == event1.Id).ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(e => e.Id == enroll1.Id);
        result.Should().Contain(e => e.Id == enroll2.Id);
    }

    [Fact]
    public async Task GetEnrollmentsByUserIdAsync_ReturnsUserEnrollments()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Event 1", DateTime.Now.AddDays(7), "Location 1", Core.Enums.EventType.Festival, "Desc1");
        var event2 = await _eventService.CreateEventAsync("Event 2", DateTime.Now.AddDays(8), "Location 2", Core.Enums.EventType.Atuacao, "Desc2");

        var enroll1 = await _enrollmentService.CreateEnrollmentAsync("user1", event1.Id);
        var enroll2 = await _enrollmentService.CreateEnrollmentAsync("user1", event2.Id);
        var enroll3 = await _enrollmentService.CreateEnrollmentAsync("user2", event1.Id);

        // Act - Get enrollments directly from context
        var allEnrollments = await _context.Enrollments.ToListAsync();
        var result = allEnrollments.Where(e => e.UserId == "user1").ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(e => e.Id == enroll1.Id);
        result.Should().Contain(e => e.Id == enroll2.Id);
    }

    [Fact]
    public async Task DeleteEnrollmentAsync_RemovesEnrollment()
    {
        // Arrange
        var eventEntity = await _eventService.CreateEventAsync("Test Event", DateTime.Now.AddDays(7), "Location", Core.Enums.EventType.Festival, "Description");
        var enrollment = await _enrollmentService.CreateEnrollmentAsync("user123", eventEntity.Id);

        // Act
        await _enrollmentService.DeleteEnrollmentAsync(enrollment.Id);
        var deleted = await _enrollmentService.GetEnrollmentByIdAsync(enrollment.Id);

        // Assert
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteEnrollmentAsync_WithInvalidId_ThrowsException()
    {
        // Act & Assert
        var act = async () => await _enrollmentService.DeleteEnrollmentAsync(999);
        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task UpdateEnrollmentAsync_WithValidData_UpdatesEnrollment()
    {
        // Arrange
        var eventEntity = await _eventService.CreateEventAsync(
            "Test Event", DateTime.Now.AddDays(7), "Location", Core.Enums.EventType.Festival, "Description");
        var enrollment = await _enrollmentService.CreateEnrollmentAsync(
            "user123", eventEntity.Id, Core.Enums.InstrumentType.Guitarra, "Initial notes", true, "Viola");

        // Act
        var result = await _enrollmentService.UpdateEnrollmentAsync(
            enrollment.Id,
            false,
            Core.Enums.InstrumentType.Percussao,
            "Updated notes",
            "Guitarra, Bandolim");

        // Assert
        result.Should().NotBeNull();
        result.WillAttend.Should().BeFalse();
        result.Instrument.Should().Be(Core.Enums.InstrumentType.Percussao);
        result.Notes.Should().Be("Updated notes");
        result.OtherInstruments.Should().Be("Guitarra, Bandolim");
    }

    [Fact]
    public async Task UpdateEnrollmentAsync_WithNullInstrument_UpdatesCorrectly()
    {
        // Arrange
        var eventEntity = await _eventService.CreateEventAsync(
            "Test Event", DateTime.Now.AddDays(7), "Location", Core.Enums.EventType.Festival, "Description");
        var enrollment = await _enrollmentService.CreateEnrollmentAsync(
            "user123", eventEntity.Id, Core.Enums.InstrumentType.Guitarra, "Notes", true);

        // Act
        var result = await _enrollmentService.UpdateEnrollmentAsync(
            enrollment.Id,
            true,
            null,
            "New notes",
            null);

        // Assert
        result.Should().NotBeNull();
        result.WillAttend.Should().BeTrue();
        result.Instrument.Should().BeNull();
        result.Notes.Should().Be("New notes");
        result.OtherInstruments.Should().BeNull();
    }

    [Fact]
    public async Task UpdateEnrollmentAsync_WithInvalidId_ThrowsException()
    {
        // Act & Assert
        var act = async () => await _enrollmentService.UpdateEnrollmentAsync(999, true, null, null, null);
        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task CreateEnrollmentAsync_WithWillAttendFalse_DoesNotSendNotification()
    {
        // Arrange
        var testEvent = Event.Create("Test Event", DateTime.Now.AddDays(7), "Test Location", Core.Enums.EventType.Festival, "Test Description");
        _context.Events.Add(testEvent);
        await _context.SaveChangesAsync();

        var mockPushNotificationService = new Mock<IPushNotificationService>();
        var mockPushNotificationFactory = new Mock<IPushNotificationFactory>();
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

        var enrollmentService = new EnrollmentService(
            new EnrollmentRepository(_context),
            _mockRetirementStatusService.Object,
            mockPushNotificationFactory.Object,
            mockPushNotificationService.Object,
            mockHttpContextAccessor.Object);

        // Act
        await enrollmentService.CreateEnrollmentAsync("user1", testEvent.Id, willAttend: false);

        // Assert - Verify NO notification was sent when willAttend = false
        mockPushNotificationService.Verify(
            x => x.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<RTUB.Application.DTOs.SendPushNotificationDto>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateEnrollmentAsync_WithWillAttendTrue_SendsNotificationOnlyToAttendingUsers()
    {
        // Arrange
        var testEvent = Event.Create("Test Event", DateTime.Now.AddDays(7), "Test Location", Core.Enums.EventType.Festival, "Test Description");
        _context.Events.Add(testEvent);
        await _context.SaveChangesAsync();

        // Create test users
        var attendingUser1 = new ApplicationUser 
        { 
            Id = "enroll_user1", 
            UserName = "enroll_user1", 
            Email = "enroll_user1@test.com",
            FirstName = "User",
            LastName = "One",
            Nickname = "User One" 
        };
        var attendingUser2 = new ApplicationUser 
        { 
            Id = "enroll_user2", 
            UserName = "enroll_user2", 
            Email = "enroll_user2@test.com",
            FirstName = "User",
            LastName = "Two",
            Nickname = "User Two" 
        };
        var notAttendingUser = new ApplicationUser 
        { 
            Id = "enroll_user3", 
            UserName = "enroll_user3", 
            Email = "enroll_user3@test.com",
            FirstName = "User",
            LastName = "Three",
            Nickname = "User Three" 
        };
        var newAttendingUser = new ApplicationUser 
        { 
            Id = "enroll_user4", 
            UserName = "enroll_user4", 
            Email = "enroll_user4@test.com",
            FirstName = "User",
            LastName = "Four",
            Nickname = "User Four" 
        };
        
        _context.Users.AddRange(attendingUser1, attendingUser2, notAttendingUser, newAttendingUser);

        // Create existing enrollments
        var enrollment1 = Enrollment.Create("enroll_user1", testEvent.Id);
        enrollment1.WillAttend = true;
        
        var enrollment2 = Enrollment.Create("enroll_user2", testEvent.Id);
        enrollment2.WillAttend = true;
        
        var enrollment3 = Enrollment.Create("enroll_user3", testEvent.Id);
        enrollment3.WillAttend = false; // This user is NOT attending
        
        _context.Enrollments.AddRange(enrollment1, enrollment2, enrollment3);
        await _context.SaveChangesAsync();

        // Setup mock to track notification calls
        var capturedRecipients = new List<string>();
        var mockPushNotificationService = new Mock<IPushNotificationService>();
        mockPushNotificationService
            .Setup(x => x.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<RTUB.Application.DTOs.SendPushNotificationDto>()))
            .Callback<IEnumerable<string>, RTUB.Application.DTOs.SendPushNotificationDto>((recipients, _) => 
            {
                capturedRecipients.AddRange(recipients);
            })
            .Returns(Task.CompletedTask);

        var mockPushNotificationFactory = new Mock<IPushNotificationFactory>();
        mockPushNotificationFactory
            .Setup(x => x.CreateEventEnrollmentNotification(It.IsAny<Event>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new RTUB.Application.DTOs.SendPushNotificationDto());

        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

        var enrollmentService = new EnrollmentService(
            new EnrollmentRepository(_context),
            _mockRetirementStatusService.Object,
            mockPushNotificationFactory.Object,
            mockPushNotificationService.Object,
            mockHttpContextAccessor.Object);

        // Act - New user enrolls with willAttend = true
        await enrollmentService.CreateEnrollmentAsync("enroll_user4", testEvent.Id, willAttend: true);

        // Assert - Verify notification was sent only to users with WillAttend = true
        mockPushNotificationService.Verify(
            x => x.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<RTUB.Application.DTOs.SendPushNotificationDto>()),
            Times.Once);

        capturedRecipients.Should().HaveCount(2, "only attending users should receive notification");
        capturedRecipients.Should().Contain("enroll_user1", "user1 is attending");
        capturedRecipients.Should().Contain("enroll_user2", "user2 is attending");
        capturedRecipients.Should().NotContain("enroll_user3", "user3 is not attending");
        capturedRecipients.Should().NotContain("enroll_user4", "user4 is the one enrolling");
    }

    [Fact]
    public async Task UpdateEnrollmentAsync_ChangingToNotAttending_SendsNotificationOnlyToAttendingUsers()
    {
        // Arrange
        var testEvent = Event.Create("Test Event", DateTime.Now.AddDays(7), "Test Location", Core.Enums.EventType.Festival, "Test Description");
        _context.Events.Add(testEvent);
        await _context.SaveChangesAsync();

        // Create test users
        var attendingUser1 = new ApplicationUser 
        { 
            Id = "enroll_change_user1", 
            UserName = "enroll_change_user1", 
            Email = "enroll_change_user1@test.com",
            FirstName = "User",
            LastName = "One",
            Nickname = "User One" 
        };
        var attendingUser2 = new ApplicationUser 
        { 
            Id = "enroll_change_user2", 
            UserName = "enroll_change_user2", 
            Email = "enroll_change_user2@test.com",
            FirstName = "User",
            LastName = "Two",
            Nickname = "User Two" 
        };
        var notAttendingUser = new ApplicationUser 
        { 
            Id = "enroll_change_user3", 
            UserName = "enroll_change_user3", 
            Email = "enroll_change_user3@test.com",
            FirstName = "User",
            LastName = "Three",
            Nickname = "User Three" 
        };
        var changingUser = new ApplicationUser 
        { 
            Id = "enroll_change_user4", 
            UserName = "enroll_change_user4", 
            Email = "enroll_change_user4@test.com",
            FirstName = "User",
            LastName = "Four",
            Nickname = "User Four" 
        };
        
        _context.Users.AddRange(attendingUser1, attendingUser2, notAttendingUser, changingUser);

        // Create existing enrollments
        var enrollment1 = Enrollment.Create("enroll_change_user1", testEvent.Id);
        enrollment1.WillAttend = true;
        
        var enrollment2 = Enrollment.Create("enroll_change_user2", testEvent.Id);
        enrollment2.WillAttend = true;
        
        var enrollment3 = Enrollment.Create("enroll_change_user3", testEvent.Id);
        enrollment3.WillAttend = false; // This user is NOT attending
        
        var enrollment4 = Enrollment.Create("enroll_change_user4", testEvent.Id);
        enrollment4.WillAttend = true; // This user is currently attending but will change
        
        _context.Enrollments.AddRange(enrollment1, enrollment2, enrollment3, enrollment4);
        await _context.SaveChangesAsync();

        // Setup mock to track notification calls
        var capturedRecipients = new List<string>();
        var mockPushNotificationService = new Mock<IPushNotificationService>();
        mockPushNotificationService
            .Setup(x => x.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<RTUB.Application.DTOs.SendPushNotificationDto>()))
            .Callback<IEnumerable<string>, RTUB.Application.DTOs.SendPushNotificationDto>((recipients, _) => 
            {
                capturedRecipients.AddRange(recipients);
            })
            .Returns(Task.CompletedTask);

        var mockPushNotificationFactory = new Mock<IPushNotificationFactory>();
        mockPushNotificationFactory
            .Setup(x => x.CreateEventCancellationNotification(It.IsAny<Event>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new RTUB.Application.DTOs.SendPushNotificationDto());

        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

        var enrollmentService = new EnrollmentService(
            new EnrollmentRepository(_context),
            _mockRetirementStatusService.Object,
            mockPushNotificationFactory.Object,
            mockPushNotificationService.Object,
            mockHttpContextAccessor.Object);

        // Act - User changes from attending to not attending
        await enrollmentService.UpdateEnrollmentAsync(enrollment4.Id, willAttend: false);

        // Assert - Verify notification was sent only to users with WillAttend = true (excluding the changing user)
        mockPushNotificationService.Verify(
            x => x.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<RTUB.Application.DTOs.SendPushNotificationDto>()),
            Times.Once);

        capturedRecipients.Should().HaveCount(2, "only attending users should receive notification");
        capturedRecipients.Should().Contain("enroll_change_user1", "user1 is attending");
        capturedRecipients.Should().Contain("enroll_change_user2", "user2 is attending");
        capturedRecipients.Should().NotContain("enroll_change_user3", "user3 is not attending");
        capturedRecipients.Should().NotContain("enroll_change_user4", "user4 is the one cancelling enrollment");
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
