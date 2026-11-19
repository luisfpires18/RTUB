// RehearsalAttendanceService: notifications.
using FluentAssertions;
using Moq;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

public partial class RehearsalAttendanceServiceTests
{
    [Fact]
    public async Task MarkAttendanceAsync_NonAttendance_SendsNotificationOnlyToAttendingUsers()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        // Create test users
        var attendingUser1 = new ApplicationUser
        {
            Id = "user1",
            UserName = "user1",
            Email = "user1@test.com",
            FirstName = "User",
            LastName = "One",
            Nickname = "User One"
        };
        var attendingUser2 = new ApplicationUser
        {
            Id = "user2",
            UserName = "user2",
            Email = "user2@test.com",
            FirstName = "User",
            LastName = "Two",
            Nickname = "User Two"
        };
        var notAttendingUser = new ApplicationUser
        {
            Id = "user3",
            UserName = "user3",
            Email = "user3@test.com",
            FirstName = "User",
            LastName = "Three",
            Nickname = "User Three"
        };
        var newNonAttendingUser = new ApplicationUser
        {
            Id = "user4",
            UserName = "user4",
            Email = "user4@test.com",
            FirstName = "User",
            LastName = "Four",
            Nickname = "User Four"
        };

        _context.Users.AddRange(attendingUser1, attendingUser2, notAttendingUser, newNonAttendingUser);

        // Create existing attendances
        var attendance1 = RehearsalAttendance.Create(rehearsal.Id, "user1", InstrumentType.Guitarra);
        attendance1.WillAttend = true;

        var attendance2 = RehearsalAttendance.Create(rehearsal.Id, "user2", InstrumentType.Baixo);
        attendance2.WillAttend = true;

        var attendance3 = RehearsalAttendance.Create(rehearsal.Id, "user3", InstrumentType.Bandolim);
        attendance3.WillAttend = false; // This user is NOT attending

        _context.RehearsalAttendances.AddRange(attendance1, attendance2, attendance3);
        await _context.SaveChangesAsync();

        // Setup mock to track notification calls
        var capturedRecipients = new List<string>();
        _mockPushNotificationService
            .Setup(x => x.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<RTUB.Application.DTOs.SendPushNotificationDto>()))
            .Callback<IEnumerable<string>, RTUB.Application.DTOs.SendPushNotificationDto>((recipients, _) =>
            {
                capturedRecipients.AddRange(recipients);
            })
            .ReturnsAsync((0, 0));

        _mockPushNotificationFactory
            .Setup(x => x.CreateRehearsalNonAttendanceNotification(It.IsAny<Rehearsal>(), It.IsAny<string>()))
            .Returns(new RTUB.Application.DTOs.SendPushNotificationDto());

        // Act - New user marks non-attendance
        await _attendanceService.MarkAttendanceAsync(rehearsal.Id, "user4", willAttend: false);

        // Assert - Verify notification was sent only to users with WillAttend = true
        _mockPushNotificationService.Verify(
            x => x.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<RTUB.Application.DTOs.SendPushNotificationDto>()),
            Times.Once);

        capturedRecipients.Should().HaveCount(2, "only attending users should receive notification");
        capturedRecipients.Should().Contain("user1", "user1 is attending");
        capturedRecipients.Should().Contain("user2", "user2 is attending");
        capturedRecipients.Should().NotContain("user3", "user3 is not attending");
        capturedRecipients.Should().NotContain("user4", "user4 is the one marking non-attendance");
    }

    [Fact]
    public async Task MarkAttendanceAsync_Attendance_SendsNotificationOnlyToAttendingUsers()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        // Create test users
        var attendingUser1 = new ApplicationUser
        {
            Id = "user1",
            UserName = "user1",
            Email = "user1@test.com",
            FirstName = "User",
            LastName = "One",
            Nickname = "User One"
        };
        var attendingUser2 = new ApplicationUser
        {
            Id = "user2",
            UserName = "user2",
            Email = "user2@test.com",
            FirstName = "User",
            LastName = "Two",
            Nickname = "User Two"
        };
        var notAttendingUser = new ApplicationUser
        {
            Id = "user3",
            UserName = "user3",
            Email = "user3@test.com",
            FirstName = "User",
            LastName = "Three",
            Nickname = "User Three"
        };
        var newAttendingUser = new ApplicationUser
        {
            Id = "user4",
            UserName = "user4",
            Email = "user4@test.com",
            FirstName = "User",
            LastName = "Four",
            Nickname = "User Four"
        };

        _context.Users.AddRange(attendingUser1, attendingUser2, notAttendingUser, newAttendingUser);

        // Create existing attendances
        var attendance1 = RehearsalAttendance.Create(rehearsal.Id, "user1", InstrumentType.Guitarra);
        attendance1.WillAttend = true;

        var attendance2 = RehearsalAttendance.Create(rehearsal.Id, "user2", InstrumentType.Baixo);
        attendance2.WillAttend = true;

        var attendance3 = RehearsalAttendance.Create(rehearsal.Id, "user3", InstrumentType.Bandolim);
        attendance3.WillAttend = false; // This user is NOT attending

        _context.RehearsalAttendances.AddRange(attendance1, attendance2, attendance3);
        await _context.SaveChangesAsync();

        // Setup mock to track notification calls
        var capturedRecipients = new List<string>();
        _mockPushNotificationService
            .Setup(x => x.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<RTUB.Application.DTOs.SendPushNotificationDto>()))
            .Callback<IEnumerable<string>, RTUB.Application.DTOs.SendPushNotificationDto>((recipients, _) =>
            {
                capturedRecipients.AddRange(recipients);
            })
            .ReturnsAsync((0, 0));

        _mockPushNotificationFactory
            .Setup(x => x.CreateRehearsalAttendanceNotification(It.IsAny<Rehearsal>(), It.IsAny<string>()))
            .Returns(new RTUB.Application.DTOs.SendPushNotificationDto());

        // Act - New user marks attendance
        await _attendanceService.MarkAttendanceAsync(rehearsal.Id, "user4", willAttend: true);

        // Assert - Verify notification was sent only to users with WillAttend = true
        _mockPushNotificationService.Verify(
            x => x.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<RTUB.Application.DTOs.SendPushNotificationDto>()),
            Times.Once);

        capturedRecipients.Should().HaveCount(2, "only attending users should receive notification");
        capturedRecipients.Should().Contain("user1", "user1 is attending");
        capturedRecipients.Should().Contain("user2", "user2 is attending");
        capturedRecipients.Should().NotContain("user3", "user3 is not attending");
        capturedRecipients.Should().NotContain("user4", "user4 is the one marking attendance");
    }

    [Fact]
    public async Task MarkAttendanceAsync_ChangingToNonAttendance_SendsNotificationOnlyToAttendingUsers()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        // Create test users
        var attendingUser1 = new ApplicationUser
        {
            Id = "user1",
            UserName = "user1",
            Email = "user1@test.com",
            FirstName = "User",
            LastName = "One",
            Nickname = "User One"
        };
        var attendingUser2 = new ApplicationUser
        {
            Id = "user2",
            UserName = "user2",
            Email = "user2@test.com",
            FirstName = "User",
            LastName = "Two",
            Nickname = "User Two"
        };
        var notAttendingUser = new ApplicationUser
        {
            Id = "user3",
            UserName = "user3",
            Email = "user3@test.com",
            FirstName = "User",
            LastName = "Three",
            Nickname = "User Three"
        };
        var changingUser = new ApplicationUser
        {
            Id = "user4",
            UserName = "user4",
            Email = "user4@test.com",
            FirstName = "User",
            LastName = "Four",
            Nickname = "User Four"
        };

        _context.Users.AddRange(attendingUser1, attendingUser2, notAttendingUser, changingUser);

        // Create existing attendances
        var attendance1 = RehearsalAttendance.Create(rehearsal.Id, "user1", InstrumentType.Guitarra);
        attendance1.WillAttend = true;

        var attendance2 = RehearsalAttendance.Create(rehearsal.Id, "user2", InstrumentType.Baixo);
        attendance2.WillAttend = true;

        var attendance3 = RehearsalAttendance.Create(rehearsal.Id, "user3", InstrumentType.Bandolim);
        attendance3.WillAttend = false; // This user is NOT attending

        var attendance4 = RehearsalAttendance.Create(rehearsal.Id, "user4", InstrumentType.Cavaquinho);
        attendance4.WillAttend = true; // This user is currently attending but will change

        _context.RehearsalAttendances.AddRange(attendance1, attendance2, attendance3, attendance4);
        await _context.SaveChangesAsync();

        // Setup mock to track notification calls
        var capturedRecipients = new List<string>();
        _mockPushNotificationService
            .Setup(x => x.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<RTUB.Application.DTOs.SendPushNotificationDto>()))
            .Callback<IEnumerable<string>, RTUB.Application.DTOs.SendPushNotificationDto>((recipients, _) =>
            {
                capturedRecipients.AddRange(recipients);
            })
            .ReturnsAsync((0, 0));

        _mockPushNotificationFactory
            .Setup(x => x.CreateRehearsalCancellationNotification(It.IsAny<Rehearsal>(), It.IsAny<string>()))
            .Returns(new RTUB.Application.DTOs.SendPushNotificationDto());

        // Act - User changes from attending to not attending
        await _attendanceService.MarkAttendanceAsync(rehearsal.Id, "user4", willAttend: false);

        // Assert - Verify notification was sent only to users with WillAttend = true (excluding the changing user)
        _mockPushNotificationService.Verify(
            x => x.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<RTUB.Application.DTOs.SendPushNotificationDto>()),
            Times.Once);

        capturedRecipients.Should().HaveCount(2, "only attending users should receive notification");
        capturedRecipients.Should().Contain("user1", "user1 is attending");
        capturedRecipients.Should().Contain("user2", "user2 is attending");
        capturedRecipients.Should().NotContain("user3", "user3 is not attending");
        capturedRecipients.Should().NotContain("user4", "user4 is the one cancelling attendance");
    }
}
