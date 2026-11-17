using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Application.Repositories;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Tests.Services;

// EnrollmentServiceTests: Update Enrollment tests.
public partial class EnrollmentServiceTests
{
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
            x => x.ReconcileMemberStatusAsync("user123"),
            Times.Once,
            "Retirement status should be updated when enrollment changes to attending");
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
            .Setup(x => x.CreateEventCancellationNotification(It.IsAny<Event>(), It.IsAny<string>()))
            .Returns(new RTUB.Application.DTOs.SendPushNotificationDto());

        var enrollmentService = new EnrollmentService(
            new EnrollmentRepository(_fixture.CreateContextFactory()),
            _mockRetirementStatusService.Object,
            mockPushNotificationFactory.Object,
            mockPushNotificationService.Object,
            CreateMockUserManager().Object);

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
}
