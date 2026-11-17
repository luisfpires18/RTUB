using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Application.Repositories;
using RTUB.Application.Services;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

// EnrollmentServiceTests: Create Enrollment tests.
public partial class EnrollmentServiceTests
{
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
            .Setup(x => x.CreateEventEnrollmentNotification(It.IsAny<Event>(), It.IsAny<string>()))
            .Returns(new RTUB.Application.DTOs.SendPushNotificationDto());

        var enrollmentService = new EnrollmentService(
            new EnrollmentRepository(_fixture.CreateContextFactory()),
            _mockRetirementStatusService.Object,
            mockPushNotificationFactory.Object,
            mockPushNotificationService.Object,
            CreateMockUserManager().Object);

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
    public async Task CreateEnrollmentAsync_WithSkipNotificationTrue_DoesNotSendNotification()
    {
        // Arrange
        var testEvent = Event.Create("Test Event", DateTime.Now.AddDays(7), "Test Location", Core.Enums.EventType.Festival, "Test Description");
        _context.Events.Add(testEvent);
        await _context.SaveChangesAsync();

        var mockPushNotificationService = new Mock<IPushNotificationService>();
        var mockPushNotificationFactory = new Mock<IPushNotificationFactory>();

        var enrollmentService = new EnrollmentService(
            new EnrollmentRepository(_fixture.CreateContextFactory()),
            _mockRetirementStatusService.Object,
            mockPushNotificationFactory.Object,
            mockPushNotificationService.Object,
            CreateMockUserManager().Object);

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

        var enrollmentService = new EnrollmentService(
            new EnrollmentRepository(_fixture.CreateContextFactory()),
            _mockRetirementStatusService.Object,
            mockPushNotificationFactory.Object,
            mockPushNotificationService.Object,
            CreateMockUserManager().Object);

        // Act - Create enrollment for past event
        var result = await enrollmentService.CreateEnrollmentAsync("user123", pastEvent.Id, willAttend: true);

        // Assert - Verify no notification was sent for past event
        mockPushNotificationService.Verify(
            x => x.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<RTUB.Application.DTOs.SendPushNotificationDto>()),
            Times.Never);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateEnrollmentAsync_SnapshotsUserPrimaryCategoryAtEvent()
    {
        // Arrange — seed user with Leitao category, service must snapshot Leitao on enrollment row
        var user = new ApplicationUser
        {
            Id = "snapshot-user-1",
            FirstName = "Snap",
            LastName = "User",
            Nickname = "Snap",
            Email = "snap1@example.com",
            UserName = "snap1@example.com",
            Categories = new List<Core.Enums.MemberCategory> { Core.Enums.MemberCategory.Leitao }
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var mockUserManager = CreateMockUserManager();
        mockUserManager.Setup(m => m.FindByIdAsync(user.Id)).ReturnsAsync(user);

        var mockPushNotificationFactory = new Mock<IPushNotificationFactory>();
        var mockPushNotificationService = new Mock<IPushNotificationService>();

        var service = new EnrollmentService(
            new EnrollmentRepository(_fixture.CreateContextFactory()),
            _mockRetirementStatusService.Object,
            mockPushNotificationFactory.Object,
            mockPushNotificationService.Object,
            mockUserManager.Object);

        var eventEntity = await _eventService.CreateEventAsync(
            "Snapshot Event", DateTime.Now.AddDays(7), "Loc", Core.Enums.EventType.Festival, "Desc");

        // Act
        var enrollment = await service.CreateEnrollmentAsync(user.Id, eventEntity.Id, skipNotification: true);

        // Assert
        enrollment.CategoryAtEvent.Should().Be(Core.Enums.MemberCategory.Leitao);
    }

    [Fact]
    public async Task CreateEnrollmentAsync_PicksHighestPriorityCategoryForSnapshot()
    {
        var user = new ApplicationUser
        {
            Id = "snapshot-user-2",
            FirstName = "Snap",
            LastName = "Two",
            Nickname = "Snap2",
            Email = "snap2@example.com",
            UserName = "snap2@example.com",
            Categories = new List<Core.Enums.MemberCategory>
            {
                Core.Enums.MemberCategory.Tuno,
                Core.Enums.MemberCategory.Veterano,
                Core.Enums.MemberCategory.Tunossauro
            }
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var mockUserManager = CreateMockUserManager();
        mockUserManager.Setup(m => m.FindByIdAsync(user.Id)).ReturnsAsync(user);

        var service = new EnrollmentService(
            new EnrollmentRepository(_fixture.CreateContextFactory()),
            _mockRetirementStatusService.Object,
            new Mock<IPushNotificationFactory>().Object,
            new Mock<IPushNotificationService>().Object,
            mockUserManager.Object);

        var eventEntity = await _eventService.CreateEventAsync(
            "Snapshot Event 2", DateTime.Now.AddDays(8), "Loc", Core.Enums.EventType.Festival, "Desc");

        var enrollment = await service.CreateEnrollmentAsync(user.Id, eventEntity.Id, skipNotification: true);

        enrollment.CategoryAtEvent.Should().Be(Core.Enums.MemberCategory.Tunossauro);
    }

    [Fact]
    public async Task CreateEnrollmentAsync_UserNotFound_SnapshotsNull()
    {
        var mockUserManager = CreateMockUserManager();
        mockUserManager.Setup(m => m.FindByIdAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);

        var service = new EnrollmentService(
            new EnrollmentRepository(_fixture.CreateContextFactory()),
            _mockRetirementStatusService.Object,
            new Mock<IPushNotificationFactory>().Object,
            new Mock<IPushNotificationService>().Object,
            mockUserManager.Object);

        var eventEntity = await _eventService.CreateEventAsync(
            "Snapshot Event Null", DateTime.Now.AddDays(9), "Loc", Core.Enums.EventType.Festival, "Desc");

        var enrollment = await service.CreateEnrollmentAsync("nonexistent-user", eventEntity.Id, skipNotification: true);

        enrollment.CategoryAtEvent.Should().BeNull();
    }
}
