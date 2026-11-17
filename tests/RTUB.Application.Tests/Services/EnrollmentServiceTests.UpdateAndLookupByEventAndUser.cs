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

// EnrollmentServiceTests: Update And Lookup By Event And User tests.
public partial class EnrollmentServiceTests
{
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
            .Setup(x => x.CreateEventEnrollmentNotification(It.IsAny<Event>(), It.IsAny<string>()))
            .Returns(new RTUB.Application.DTOs.SendPushNotificationDto());

        var mockRetirementStatusService = new Mock<IMemberStatusService>();

        var enrollmentService = new EnrollmentService(
            new EnrollmentRepository(_fixture.CreateContextFactory()),
            mockRetirementStatusService.Object,
            mockPushNotificationFactory.Object,
            mockPushNotificationService.Object,
            CreateMockUserManager().Object);

        // Act - User changes from not attending to attending
        await enrollmentService.UpdateEnrollmentAsync(enrollment2.Id, willAttend: true);

        // Assert - Verify notification was sent and retirement status was updated
        mockPushNotificationService.Verify(
            x => x.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<RTUB.Application.DTOs.SendPushNotificationDto>()),
            Times.Once);

        mockRetirementStatusService.Verify(
            x => x.ReconcileMemberStatusAsync("enroll_attend_user2"),
            Times.Once,
            "Retirement status should be updated when enrollment changes to attending");

        capturedRecipients.Should().HaveCount(1, "only attending users should receive notification");
        capturedRecipients.Should().Contain("enroll_attend_user1", "user1 is attending");
        capturedRecipients.Should().NotContain("enroll_attend_user2", "user2 is the one enrolling");
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

        var enrollmentService = new EnrollmentService(
            new EnrollmentRepository(_fixture.CreateContextFactory()),
            _mockRetirementStatusService.Object,
            mockPushNotificationFactory.Object,
            mockPushNotificationService.Object,
            CreateMockUserManager().Object);

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

        var enrollmentService = new EnrollmentService(
            new EnrollmentRepository(_fixture.CreateContextFactory()),
            _mockRetirementStatusService.Object,
            mockPushNotificationFactory.Object,
            mockPushNotificationService.Object,
            CreateMockUserManager().Object);

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
}
