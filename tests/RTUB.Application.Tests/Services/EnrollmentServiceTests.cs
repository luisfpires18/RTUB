using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Repositories;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for EnrollmentService
/// Tests enrollment CRUD operations
/// </summary>
public partial class EnrollmentServiceTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private static Mock<UserManager<ApplicationUser>> CreateMockUserManager()
    {
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly EnrollmentService _enrollmentService;
    private readonly EventRepository _eventRepository;
    private readonly EventService _eventService;
    private readonly Mock<IImageStorageService> _mockImageStorageService;
    private readonly Mock<IMemberStatusService> _mockRetirementStatusService;

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
        _mockRetirementStatusService = new Mock<IMemberStatusService>();

        var mockPushNotificationFactory = new Mock<IPushNotificationFactory>();
        var mockPushNotificationService = new Mock<IPushNotificationService>();

        _enrollmentService = new EnrollmentService(
            new EnrollmentRepository(_fixture.CreateContextFactory()),
            _mockRetirementStatusService.Object,
            mockPushNotificationFactory.Object,
            mockPushNotificationService.Object,
            CreateMockUserManager().Object);

        _eventRepository = new EventRepository(_fixture.CreateContextFactory());
        var mockEventVideoRepository = new Mock<IEventVideoRepository>();
        var mockEventVideoStorageService = new Mock<IEventVideoStorageService>();

        // Mock UserManager
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        var mockUserManager = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _eventService = new EventService(
            _eventRepository,
            _mockImageStorageService.Object,
            new EnrollmentRepository(_fixture.CreateContextFactory()),
            mockEventVideoRepository.Object,
            mockEventVideoStorageService.Object,
            mockPushNotificationFactory.Object,
            mockPushNotificationService.Object,
            mockUserManager.Object,
            _fixture.CreateContextFactory());
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
            .Setup(x => x.CreateEventNonEnrollmentNotification(It.IsAny<Event>(), It.IsAny<string>()))
            .Returns(new RTUB.Application.DTOs.SendPushNotificationDto());

        var enrollmentService = new EnrollmentService(
            new EnrollmentRepository(_fixture.CreateContextFactory()),
            _mockRetirementStatusService.Object,
            mockPushNotificationFactory.Object,
            mockPushNotificationService.Object,
            CreateMockUserManager().Object);

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

    public void Dispose()
    {
        _context?.Dispose();
    }
}
