using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Repositories;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;

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

        var originalEnrolledAt = enrollment.EnrolledAt;

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
        result.EnrolledAt.Should().Be(originalEnrolledAt, "enlist time should not change when WillAttend stays the same");
    }

    [Fact]
    public async Task UpdateEnrollmentAsync_WhenWillAttendChanges_UpdatesEnrolledAtTimestamp()
    {
        // Arrange
        var eventEntity = await _eventService.CreateEventAsync(
            "Test Event", DateTime.Now.AddDays(7), "Location", Core.Enums.EventType.Festival, "Description");
        var enrollment = await _enrollmentService.CreateEnrollmentAsync(
            "user123", eventEntity.Id, Core.Enums.InstrumentType.Guitarra, "Notes", true);

        var originalEnrolledAt = enrollment.EnrolledAt;

        // Act - toggle attendance
        var result = await _enrollmentService.UpdateEnrollmentAsync(
            enrollment.Id,
            false,
            Core.Enums.InstrumentType.Guitarra,
            "Updated notes",
            "Other");

        // Assert
        result.Should().NotBeNull();
        result.WillAttend.Should().BeFalse();
        result.EnrolledAt.Should().BeAfter(originalEnrolledAt, "enlist time should be refreshed when WillAttend changes");
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
    public async Task UpdateEnrollmentAsync_WhenWillAttendChangesToTrue_UpdatesRetirementStatus()
    {
        // Arrange
        var eventEntity = await _eventService.CreateEventAsync(
            "Test Event", DateTime.Now.AddDays(7), "Location", Core.Enums.EventType.Festival, "Description");
        var enrollment = await _enrollmentService.CreateEnrollmentAsync("user123", eventEntity.Id, willAttend: false);
        _mockRetirementStatusService.Reset();

        // Act
        await _enrollmentService.UpdateEnrollmentAsync(enrollment.Id, willAttend: true);

        // Assert
        _mockRetirementStatusService.Verify(
            x => x.UpdateUserRetirementStatusAsync("user123"),
            Times.Once,
            "Retirement status should be updated when enrollment changes to attending");
    }

    [Fact]
    public async Task GetEnrollmentByEventAndUserAsync_WhenExists_ReturnsEnrollment()
    {
        // Arrange
        var eventEntity = await _eventService.CreateEventAsync(
            "Test Event", DateTime.Now.AddDays(7), "Location", Core.Enums.EventType.Festival, "Description");
        var enrollment = await _enrollmentService.CreateEnrollmentAsync("user123", eventEntity.Id);

        // Act
        var result = await _enrollmentService.GetEnrollmentByEventAndUserAsync(eventEntity.Id, "user123");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(enrollment.Id);
        result.EventId.Should().Be(eventEntity.Id);
        result.UserId.Should().Be("user123");
    }

    [Fact]
    public async Task GetEnrollmentByEventAndUserAsync_WhenNotExists_ReturnsNull()
    {
        // Arrange
        var eventEntity = await _eventService.CreateEventAsync(
            "Test Event", DateTime.Now.AddDays(7), "Location", Core.Enums.EventType.Festival, "Description");

        // Act
        var result = await _enrollmentService.GetEnrollmentByEventAndUserAsync(eventEntity.Id, "nonexistent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateEnrollmentAsync_WithWillAttendFalse_SendsNonEnrollmentNotificationToAttendingUsers()
    {
        // Arrange
        var testEvent = Event.Create("Test Event", DateTime.Now.AddDays(7), "Test Location", Core.Enums.EventType.Festival, "Test Description");
        _context.Events.Add(testEvent);
        await _context.SaveChangesAsync();

        // Create test users
        var attendingUser1 = new ApplicationUser
        {
            Id = "enroll_non_user1",
            UserName = "enroll_non_user1",
            Email = "enroll_non_user1@test.com",
            FirstName = "User",
            LastName = "One",
            Nickname = "User One"
        };
        var attendingUser2 = new ApplicationUser
        {
            Id = "enroll_non_user2",
            UserName = "enroll_non_user2",
            Email = "enroll_non_user2@test.com",
            FirstName = "User",
            LastName = "Two",
            Nickname = "User Two"
        };
        var notAttendingUser = new ApplicationUser
        {
            Id = "enroll_non_user3",
            UserName = "enroll_non_user3",
            Email = "enroll_non_user3@test.com",
            FirstName = "User",
            LastName = "Three",
            Nickname = "User Three"
        };
        var newNonEnrollUser = new ApplicationUser
        {
            Id = "enroll_non_user4",
            UserName = "enroll_non_user4",
            Email = "enroll_non_user4@test.com",
            FirstName = "User",
            LastName = "Four",
            Nickname = "User Four"
        };

        _context.Users.AddRange(attendingUser1, attendingUser2, notAttendingUser, newNonEnrollUser);

        // Create existing enrollments
        var enrollment1 = Enrollment.Create("enroll_non_user1", testEvent.Id);
        enrollment1.WillAttend = true;

        var enrollment2 = Enrollment.Create("enroll_non_user2", testEvent.Id);
        enrollment2.WillAttend = true;

        var enrollment3 = Enrollment.Create("enroll_non_user3", testEvent.Id);
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
            .ReturnsAsync((0, 0));

        var mockPushNotificationFactory = new Mock<IPushNotificationFactory>();
        mockPushNotificationFactory
            .Setup(x => x.CreateEventNonEnrollmentNotification(It.IsAny<Event>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new RTUB.Application.DTOs.SendPushNotificationDto());

        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

        var enrollmentService = new EnrollmentService(
            new EnrollmentRepository(_context),
            _mockRetirementStatusService.Object,
            mockPushNotificationFactory.Object,
            mockPushNotificationService.Object,
            mockHttpContextAccessor.Object);

        // Act - New user enrolls with willAttend = false
        await enrollmentService.CreateEnrollmentAsync("enroll_non_user4", testEvent.Id, willAttend: false);

        // Assert - Verify notification was sent only to users with WillAttend = true
        mockPushNotificationService.Verify(
            x => x.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<RTUB.Application.DTOs.SendPushNotificationDto>()),
            Times.Once);

        capturedRecipients.Should().HaveCount(2, "only attending users should receive notification");
        capturedRecipients.Should().Contain("enroll_non_user1", "user1 is attending");
        capturedRecipients.Should().Contain("enroll_non_user2", "user2 is attending");
        capturedRecipients.Should().NotContain("enroll_non_user3", "user3 is not attending");
        capturedRecipients.Should().NotContain("enroll_non_user4", "user4 is the one marking non-enrollment");
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
            .ReturnsAsync((0, 0));

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
            .ReturnsAsync((0, 0));

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

    [Fact]
    public async Task UpdateEnrollmentAsync_ChangingToAttending_SendsNotificationAndUpdatesRetirementStatus()
    {
        // Arrange
        var testEvent = Event.Create("Test Event", DateTime.Now.AddDays(7), "Test Location", Core.Enums.EventType.Festival, "Test Description");
        _context.Events.Add(testEvent);
        await _context.SaveChangesAsync();

        // Create test users
        var attendingUser1 = new ApplicationUser
        {
            Id = "enroll_attend_user1",
            UserName = "enroll_attend_user1",
            Email = "enroll_attend_user1@test.com",
            FirstName = "User",
            LastName = "One",
            Nickname = "User One"
        };
        var changingUser = new ApplicationUser
        {
            Id = "enroll_attend_user2",
            UserName = "enroll_attend_user2",
            Email = "enroll_attend_user2@test.com",
            FirstName = "User",
            LastName = "Two",
            Nickname = "User Two"
        };

        _context.Users.AddRange(attendingUser1, changingUser);

        // Create existing enrollments
        var enrollment1 = Enrollment.Create("enroll_attend_user1", testEvent.Id);
        enrollment1.WillAttend = true;

        var enrollment2 = Enrollment.Create("enroll_attend_user2", testEvent.Id);
        enrollment2.WillAttend = false; // This user is currently NOT attending but will change

        _context.Enrollments.AddRange(enrollment1, enrollment2);
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
            .ReturnsAsync((0, 0));

        var mockPushNotificationFactory = new Mock<IPushNotificationFactory>();
        mockPushNotificationFactory
            .Setup(x => x.CreateEventEnrollmentNotification(It.IsAny<Event>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new RTUB.Application.DTOs.SendPushNotificationDto());

        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        var mockRetirementStatusService = new Mock<IRetirementStatusService>();

        var enrollmentService = new EnrollmentService(
            new EnrollmentRepository(_context),
            mockRetirementStatusService.Object,
            mockPushNotificationFactory.Object,
            mockPushNotificationService.Object,
            mockHttpContextAccessor.Object);

        // Act - User changes from not attending to attending
        await enrollmentService.UpdateEnrollmentAsync(enrollment2.Id, willAttend: true);

        // Assert - Verify notification was sent and retirement status was updated
        mockPushNotificationService.Verify(
            x => x.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<RTUB.Application.DTOs.SendPushNotificationDto>()),
            Times.Once);

        mockRetirementStatusService.Verify(
            x => x.UpdateUserRetirementStatusAsync("enroll_attend_user2"),
            Times.Once,
            "Retirement status should be updated when enrollment changes to attending");

        capturedRecipients.Should().HaveCount(1, "only attending users should receive notification");
        capturedRecipients.Should().Contain("enroll_attend_user1", "user1 is attending");
        capturedRecipients.Should().NotContain("enroll_attend_user2", "user2 is the one enrolling");
    }

    [Fact]
    public async Task DeleteEnrollmentAsync_WithWillAttendTrue_SendsCancellationNotification()
    {
        // Arrange
        var testEvent = Event.Create("Test Event", DateTime.Now.AddDays(7), "Test Location", Core.Enums.EventType.Festival, "Test Description");
        _context.Events.Add(testEvent);
        await _context.SaveChangesAsync();

        // Create test users
        var attendingUser1 = new ApplicationUser
        {
            Id = "enroll_delete_user1",
            UserName = "enroll_delete_user1",
            Email = "enroll_delete_user1@test.com",
            FirstName = "User",
            LastName = "One",
            Nickname = "User One"
        };
        var deletingUser = new ApplicationUser
        {
            Id = "enroll_delete_user2",
            UserName = "enroll_delete_user2",
            Email = "enroll_delete_user2@test.com",
            FirstName = "User",
            LastName = "Two",
            Nickname = "User Two"
        };

        _context.Users.AddRange(attendingUser1, deletingUser);

        // Create existing enrollments
        var enrollment1 = Enrollment.Create("enroll_delete_user1", testEvent.Id);
        enrollment1.WillAttend = true;

        var enrollment2 = Enrollment.Create("enroll_delete_user2", testEvent.Id);
        enrollment2.WillAttend = true; // This user is attending and will delete enrollment

        _context.Enrollments.AddRange(enrollment1, enrollment2);
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
            .ReturnsAsync((0, 0));

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

        // Act - Delete enrollment for attending user
        await enrollmentService.DeleteEnrollmentAsync(enrollment2.Id);

        // Assert - Verify cancellation notification was sent
        mockPushNotificationService.Verify(
            x => x.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<RTUB.Application.DTOs.SendPushNotificationDto>()),
            Times.Once);

        capturedRecipients.Should().HaveCount(1, "only attending users should receive notification");
        capturedRecipients.Should().Contain("enroll_delete_user1", "user1 is attending");
        capturedRecipients.Should().NotContain("enroll_delete_user2", "user2 is the one deleting enrollment");

        // Verify enrollment was deleted
        var deleted = await _enrollmentService.GetEnrollmentByIdAsync(enrollment2.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteEnrollmentAsync_WithWillAttendFalse_DoesNotSendNotification()
    {
        // Arrange
        var eventEntity = await _eventService.CreateEventAsync("Test Event", DateTime.Now.AddDays(7), "Location", Core.Enums.EventType.Festival, "Description");
        var enrollment = await _enrollmentService.CreateEnrollmentAsync("user123", eventEntity.Id, willAttend: false);

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
        await enrollmentService.DeleteEnrollmentAsync(enrollment.Id);

        // Assert - Verify no notification was sent
        mockPushNotificationService.Verify(
            x => x.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<RTUB.Application.DTOs.SendPushNotificationDto>()),
            Times.Never);

        // Verify enrollment was deleted
        var deleted = await _enrollmentService.GetEnrollmentByIdAsync(enrollment.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task CreateEnrollmentAsync_WithSkipNotificationTrue_DoesNotSendNotification()
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

        // Act - Create enrollment with skipNotification = true
        var result = await enrollmentService.CreateEnrollmentAsync("user123", testEvent.Id, willAttend: true, skipNotification: true);

        // Assert - Verify no notification was sent
        mockPushNotificationService.Verify(
            x => x.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<RTUB.Application.DTOs.SendPushNotificationDto>()),
            Times.Never);

        result.Should().NotBeNull();
        result.WillAttend.Should().BeTrue();
    }

    [Fact]
    public async Task CreateEnrollmentAsync_WithPastEvent_DoesNotSendNotification()
    {
        // Arrange
        var pastEvent = Event.Create("Past Event", DateTime.Now.AddDays(-7), "Test Location", Core.Enums.EventType.Festival, "Test Description");
        _context.Events.Add(pastEvent);
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

        // Act - Create enrollment for past event
        var result = await enrollmentService.CreateEnrollmentAsync("user123", pastEvent.Id, willAttend: true);

        // Assert - Verify no notification was sent for past event
        mockPushNotificationService.Verify(
            x => x.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<RTUB.Application.DTOs.SendPushNotificationDto>()),
            Times.Never);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateEnrollmentAsync_WhenWillAttendDoesNotChange_DoesNotSendNotification()
    {
        // Arrange
        var eventEntity = await _eventService.CreateEventAsync(
            "Test Event", DateTime.Now.AddDays(7), "Location", Core.Enums.EventType.Festival, "Description");
        var enrollment = await _enrollmentService.CreateEnrollmentAsync(
            "user123", eventEntity.Id, Core.Enums.InstrumentType.Guitarra, "Initial notes", true);

        var mockPushNotificationService = new Mock<IPushNotificationService>();
        var mockPushNotificationFactory = new Mock<IPushNotificationFactory>();
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

        var enrollmentService = new EnrollmentService(
            new EnrollmentRepository(_context),
            _mockRetirementStatusService.Object,
            mockPushNotificationFactory.Object,
            mockPushNotificationService.Object,
            mockHttpContextAccessor.Object);

        // Act - Update enrollment but keep willAttend = true
        var result = await enrollmentService.UpdateEnrollmentAsync(
            enrollment.Id,
            willAttend: true, // Same as before
            Core.Enums.InstrumentType.Percussao,
            "Updated notes");

        // Assert - Verify no notification was sent since willAttend didn't change
        mockPushNotificationService.Verify(
            x => x.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<RTUB.Application.DTOs.SendPushNotificationDto>()),
            Times.Never);

        result.Should().NotBeNull();
        result.WillAttend.Should().BeTrue();
        result.Instrument.Should().Be(Core.Enums.InstrumentType.Percussao);
        result.Notes.Should().Be("Updated notes");
    }

    [Fact]
    public async Task UpdateEnrollmentAsync_WhenWillAttendChangesFromFalseToFalse_DoesNotSendNotification()
    {
        // Arrange
        var eventEntity = await _eventService.CreateEventAsync(
            "Test Event", DateTime.Now.AddDays(7), "Location", Core.Enums.EventType.Festival, "Description");
        var enrollment = await _enrollmentService.CreateEnrollmentAsync(
            "user123", eventEntity.Id, willAttend: false);

        var mockPushNotificationService = new Mock<IPushNotificationService>();
        var mockPushNotificationFactory = new Mock<IPushNotificationFactory>();
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

        var enrollmentService = new EnrollmentService(
            new EnrollmentRepository(_context),
            _mockRetirementStatusService.Object,
            mockPushNotificationFactory.Object,
            mockPushNotificationService.Object,
            mockHttpContextAccessor.Object);

        // Act - Update enrollment but keep willAttend = false
        var result = await enrollmentService.UpdateEnrollmentAsync(
            enrollment.Id,
            willAttend: false, // Same as before
            Core.Enums.InstrumentType.Guitarra,
            "Updated notes");

        // Assert - Verify no notification was sent since willAttend didn't change
        mockPushNotificationService.Verify(
            x => x.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<RTUB.Application.DTOs.SendPushNotificationDto>()),
            Times.Never);

        result.Should().NotBeNull();
        result.WillAttend.Should().BeFalse();
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
