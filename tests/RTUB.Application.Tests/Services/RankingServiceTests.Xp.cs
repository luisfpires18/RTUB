// RankingService: xp.
using FluentAssertions;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

public partial class RankingServiceTests
{
    [Fact]
    public async Task CalculateTotalXpAsync_WithNoAttendance_ReturnsZero()
    {
        // Arrange
        var userId = "user-123";

        // Act
        var result = await _service.CalculateTotalXpAsync(userId);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task CalculateTotalXpAsync_WithRehearsalAttendances_CalculatesCorrectly()
    {
        // Arrange
        var userId = "user-123";
        var rehearsal1 = new Rehearsal { Id = 1, Date = DateTime.UtcNow.AddDays(-1) };
        var rehearsal2 = new Rehearsal { Id = 2, Date = DateTime.UtcNow.AddDays(-2) };

        await _context.Rehearsals.AddRangeAsync(rehearsal1, rehearsal2);
        var attendance1 = RehearsalAttendance.Create(1, userId);
        attendance1.Attended = true;
        var attendance2 = RehearsalAttendance.Create(2, userId);
        attendance2.Attended = true;
        var attendance3 = RehearsalAttendance.Create(1, "other-user");
        attendance3.Attended = true;
        await _context.RehearsalAttendances.AddRangeAsync(attendance1, attendance2, attendance3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CalculateTotalXpAsync(userId);

        // Assert - 2 rehearsals * 10 XP = 20
        result.Should().Be(20);
    }

    [Fact]
    public async Task CalculateTotalXpAsync_WithEventEnrollments_CalculatesCorrectly()
    {
        // Arrange
        var userId = "user-123";
        var event1 = Event.Create("Event 1", DateTime.UtcNow.AddDays(-1), "Location", EventType.Atuacao);
        event1.Id = 1;
        var event2 = Event.Create("Event 2", DateTime.UtcNow.AddDays(-2), "Location", EventType.Atuacao);
        event2.Id = 2;

        await _context.Events.AddRangeAsync(event1, event2);
        var enrollment1 = Enrollment.Create(userId, 1);
        enrollment1.WillAttend = true;
        var enrollment2 = Enrollment.Create(userId, 2);
        enrollment2.WillAttend = true;
        var enrollment3 = Enrollment.Create("other-user", 1);
        enrollment3.WillAttend = true;
        await _context.Enrollments.AddRangeAsync(enrollment1, enrollment2, enrollment3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CalculateTotalXpAsync(userId);

        // Assert - 2 Atuacao events * 25 XP = 50
        result.Should().Be(50);
    }

    [Fact]
    public async Task CalculateTotalXpAsync_WithMixedAttendance_CalculatesCorrectly()
    {
        // Arrange
        var userId = "user-123";
        var rehearsal = new Rehearsal { Id = 1, Date = DateTime.UtcNow.AddDays(-1) };
        var eventEntity = Event.Create("Event 1", DateTime.UtcNow.AddDays(-1), "Location", EventType.Festival);
        eventEntity.Id = 1;

        await _context.Rehearsals.AddAsync(rehearsal);
        await _context.Events.AddAsync(eventEntity);
        var attendance = RehearsalAttendance.Create(1, userId);
        attendance.Attended = true;
        await _context.RehearsalAttendances.AddAsync(attendance);
        var enrollment = Enrollment.Create(userId, 1);
        enrollment.WillAttend = true;
        await _context.Enrollments.AddAsync(enrollment);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CalculateTotalXpAsync(userId);

        // Assert - 1 rehearsal (10 XP) + 1 Festival event (50 XP) = 60
        result.Should().Be(60);
    }

    [Fact]
    public async Task CalculateTotalXpAsync_OnlyCountsAttendedRehearsals()
    {
        // Arrange
        var userId = "user-123";
        var rehearsal1 = new Rehearsal { Id = 1, Date = DateTime.UtcNow.AddDays(-1) };
        var rehearsal2 = new Rehearsal { Id = 2, Date = DateTime.UtcNow.AddDays(-2) };

        await _context.Rehearsals.AddRangeAsync(rehearsal1, rehearsal2);
        var attendance1 = RehearsalAttendance.Create(1, userId);
        attendance1.Attended = true;
        var attendance2 = RehearsalAttendance.Create(2, userId);
        attendance2.Attended = false;
        await _context.RehearsalAttendances.AddRangeAsync(attendance1, attendance2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CalculateTotalXpAsync(userId);

        // Assert - Only 1 attended rehearsal * 10 XP = 10
        result.Should().Be(10);
    }

    [Fact]
    public async Task CalculateTotalXpAsync_OnlyCountsConfirmedEnrollments()
    {
        // Arrange
        var userId = "user-123";
        var event1 = Event.Create("Event 1", DateTime.UtcNow.AddDays(-1), "Location", EventType.Atuacao);
        event1.Id = 1;
        var event2 = Event.Create("Event 2", DateTime.UtcNow.AddDays(-2), "Location", EventType.Atuacao);
        event2.Id = 2;

        await _context.Events.AddRangeAsync(event1, event2);
        var enrollment1 = Enrollment.Create(userId, 1);
        enrollment1.WillAttend = true;
        var enrollment2 = Enrollment.Create(userId, 2);
        enrollment2.WillAttend = false;
        await _context.Enrollments.AddRangeAsync(enrollment1, enrollment2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CalculateTotalXpAsync(userId);

        // Assert - Only 1 confirmed Atuacao enrollment * 25 XP = 25
        result.Should().Be(25);
    }

    [Fact]
    public async Task CalculateTotalXpAsync_DoesNotCountFutureEvents()
    {
        // Arrange
        var userId = "user-123";
        var pastEvent = Event.Create("Past Event", DateTime.UtcNow.AddDays(-1), "Location", EventType.Atuacao);
        pastEvent.Id = 1;
        var futureEvent = Event.Create("Future Event", DateTime.UtcNow.AddDays(1), "Location", EventType.Atuacao);
        futureEvent.Id = 2;

        await _context.Events.AddRangeAsync(pastEvent, futureEvent);
        var enrollment1 = Enrollment.Create(userId, 1);
        enrollment1.WillAttend = true;
        var enrollment2 = Enrollment.Create(userId, 2);
        enrollment2.WillAttend = true;
        await _context.Enrollments.AddRangeAsync(enrollment1, enrollment2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CalculateTotalXpAsync(userId);

        // Assert - Only past Atuacao event counts: 1 event * 25 XP = 25
        result.Should().Be(25);
    }

    [Fact]
    public async Task CalculateTotalXpAsync_DoesNotCountOngoingMultiDayEvents()
    {
        // Arrange
        var userId = "user-123";
        var ongoingEvent = Event.Create("Ongoing Festival", DateTime.UtcNow.AddDays(-1), "Location", EventType.Festival);
        ongoingEvent.Id = 1;
        ongoingEvent.EndDate = DateTime.UtcNow.AddDays(1);

        await _context.Events.AddAsync(ongoingEvent);
        var enrollment = Enrollment.Create(userId, 1);
        enrollment.WillAttend = true;
        await _context.Enrollments.AddAsync(enrollment);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CalculateTotalXpAsync(userId);

        // Assert - Ongoing event should not count until after the end date
        result.Should().Be(0);
    }

    [Fact]
    public async Task GetRankProgressBatchAsync_DoesNotCountOngoingEvents()
    {
        // Arrange
        var userId = "user-123";
        var otherUserId = "other-user";
        var ongoingEvent = Event.Create("Ongoing Festival", DateTime.UtcNow.AddDays(-1), "Location", EventType.Festival);
        ongoingEvent.Id = 1;
        ongoingEvent.EndDate = DateTime.UtcNow.AddDays(1);

        var pastEvent = Event.Create("Past Festival", DateTime.UtcNow.AddDays(-3), "Location", EventType.Festival);
        pastEvent.Id = 2;

        await _context.Events.AddRangeAsync(ongoingEvent, pastEvent);

        var enrollment1 = Enrollment.Create(userId, ongoingEvent.Id);
        enrollment1.WillAttend = true;

        var enrollment2 = Enrollment.Create(otherUserId, pastEvent.Id);
        enrollment2.WillAttend = true;

        await _context.Enrollments.AddRangeAsync(enrollment1, enrollment2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetRankProgressBatchAsync(new[] { userId, otherUserId });

        // Assert
        result[userId].CurrentXp.Should().Be(0);
        result[otherUserId].CurrentXp.Should().Be(_config.XpPerEventType["Festival"]);
    }

    [Fact]
    public async Task CalculateTotalXpAsync_DoesNotCountFutureRehearsals()
    {
        // Arrange
        var userId = "user-123";
        var pastRehearsal = new Rehearsal { Id = 1, Date = DateTime.UtcNow.AddDays(-1) };
        var futureRehearsal = new Rehearsal { Id = 2, Date = DateTime.UtcNow.AddDays(1) };

        await _context.Rehearsals.AddRangeAsync(pastRehearsal, futureRehearsal);
        var attendance1 = RehearsalAttendance.Create(1, userId);
        attendance1.Attended = true;
        var attendance2 = RehearsalAttendance.Create(2, userId);
        attendance2.Attended = true;
        await _context.RehearsalAttendances.AddRangeAsync(attendance1, attendance2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CalculateTotalXpAsync(userId);

        // Assert - Only past rehearsal counts: 1 rehearsal * 10 XP = 10
        result.Should().Be(10);
    }
}
