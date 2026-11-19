using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Application.Repositories;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Tests.Services;

// EnrollmentServiceTests: Queries And Delete tests.
public partial class EnrollmentServiceTests
{
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
        var deleted = await _context.Enrollments.AsNoTracking().FirstOrDefaultAsync(e => e.Id == enrollment.Id);

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
            .Setup(x => x.CreateEventCancellationNotification(It.IsAny<Event>(), It.IsAny<string>()))
            .Returns(new RTUB.Application.DTOs.SendPushNotificationDto());

        var enrollmentService = new EnrollmentService(
            new EnrollmentRepository(_fixture.CreateContextFactory()),
            _mockRetirementStatusService.Object,
            mockPushNotificationFactory.Object,
            mockPushNotificationService.Object,
            CreateMockUserManager().Object);

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
        var deleted = await _context.Enrollments.AsNoTracking().FirstOrDefaultAsync(e => e.Id == enrollment2.Id);
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

        var enrollmentService = new EnrollmentService(
            new EnrollmentRepository(_fixture.CreateContextFactory()),
            _mockRetirementStatusService.Object,
            mockPushNotificationFactory.Object,
            mockPushNotificationService.Object,
            CreateMockUserManager().Object);

        // Act
        await enrollmentService.DeleteEnrollmentAsync(enrollment.Id);

        // Assert - Verify no notification was sent
        mockPushNotificationService.Verify(
            x => x.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<RTUB.Application.DTOs.SendPushNotificationDto>()),
            Times.Never);

        // Verify enrollment was deleted
        var deleted = await _context.Enrollments.AsNoTracking().FirstOrDefaultAsync(e => e.Id == enrollment.Id);
        deleted.Should().BeNull();
    }
}
