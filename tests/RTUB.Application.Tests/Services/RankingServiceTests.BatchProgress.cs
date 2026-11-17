// RankingService: batch progress.
using FluentAssertions;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

public partial class RankingServiceTests
{
    [Fact]
    public async Task GetRankProgressBatchAsync_WithDateRange_FiltersByDateRange()
    {
        // Arrange
        var userId = "user-123";
        var startDate = DateTime.UtcNow.AddDays(-10);
        var endDate = DateTime.UtcNow.AddDays(-5);

        // Create rehearsals within and outside the date range
        var rehearsal1 = new Rehearsal { Id = 1, Date = DateTime.UtcNow.AddDays(-8) }; // Within range
        var rehearsal2 = new Rehearsal { Id = 2, Date = DateTime.UtcNow.AddDays(-3) }; // Outside range (too recent)
        var rehearsal3 = new Rehearsal { Id = 3, Date = DateTime.UtcNow.AddDays(-12) }; // Outside range (too old)

        await _context.Rehearsals.AddRangeAsync(rehearsal1, rehearsal2, rehearsal3);
        var attendance1 = RehearsalAttendance.Create(1, userId);
        attendance1.Attended = true;
        var attendance2 = RehearsalAttendance.Create(2, userId);
        attendance2.Attended = true;
        var attendance3 = RehearsalAttendance.Create(3, userId);
        attendance3.Attended = true;
        await _context.RehearsalAttendances.AddRangeAsync(attendance1, attendance2, attendance3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetRankProgressBatchAsync(new[] { userId }, startDate, endDate);

        // Assert - Only rehearsal1 should count (within date range)
        result.Should().ContainKey(userId);
        result[userId].CurrentXp.Should().Be(10); // 1 rehearsal * 10 XP
    }

    [Fact]
    public async Task GetRankProgressBatchAsync_WithoutDateRange_ReturnsProgressForAllUsers()
    {
        // Arrange
        var userId1 = "user-1";
        var userId2 = "user-2";
        var rehearsal1 = new Rehearsal { Id = 1, Date = DateTime.UtcNow.AddDays(-1) };
        var rehearsal2 = new Rehearsal { Id = 2, Date = DateTime.UtcNow.AddDays(-2) };

        await _context.Rehearsals.AddRangeAsync(rehearsal1, rehearsal2);
        var attendance1 = RehearsalAttendance.Create(1, userId1);
        attendance1.Attended = true;
        var attendance2 = RehearsalAttendance.Create(2, userId2);
        attendance2.Attended = true;
        await _context.RehearsalAttendances.AddRangeAsync(attendance1, attendance2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetRankProgressBatchAsync(new[] { userId1, userId2 });

        // Assert
        result.Should().HaveCount(2);
        result[userId1].CurrentXp.Should().Be(10); // 1 rehearsal * 10 XP
        result[userId2].CurrentXp.Should().Be(10); // 1 rehearsal * 10 XP
        result[userId1].CurrentLevel.Should().Be(1);
        result[userId2].CurrentLevel.Should().Be(1);
    }

    [Fact]
    public async Task GetRankProgressBatchAsync_WithEmptyUserList_ReturnsEmptyDictionary()
    {
        // Act
        var result = await _service.GetRankProgressBatchAsync(Enumerable.Empty<string>());

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRankProgressBatchAsync_WithDateRange_FiltersEventsByDateRange()
    {
        // Arrange
        var userId = "user-123";
        var startDate = DateTime.UtcNow.AddDays(-10);
        var endDate = DateTime.UtcNow.AddDays(-5);

        // Create events within and outside the date range
        var event1 = Event.Create("Event 1", DateTime.UtcNow.AddDays(-8), "Location", EventType.Festival);
        event1.Id = 1; // Within range
        var event2 = Event.Create("Event 2", DateTime.UtcNow.AddDays(-3), "Location", EventType.Atuacao);
        event2.Id = 2; // Outside range (too recent)
        var event3 = Event.Create("Event 3", DateTime.UtcNow.AddDays(-12), "Location", EventType.Casamento);
        event3.Id = 3; // Outside range (too old)

        await _context.Events.AddRangeAsync(event1, event2, event3);
        var enrollment1 = Enrollment.Create(userId, 1);
        enrollment1.WillAttend = true;
        var enrollment2 = Enrollment.Create(userId, 2);
        enrollment2.WillAttend = true;
        var enrollment3 = Enrollment.Create(userId, 3);
        enrollment3.WillAttend = true;
        await _context.Enrollments.AddRangeAsync(enrollment1, enrollment2, enrollment3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetRankProgressBatchAsync(new[] { userId }, startDate, endDate);

        // Assert - Only event1 should count (within date range)
        result.Should().ContainKey(userId);
        result[userId].CurrentXp.Should().Be(50); // 1 Festival event * 50 XP
    }

    [Fact]
    public async Task GetRankProgressBatchAsync_WithMultipleUsersAndMixedAttendance_CalculatesCorrectly()
    {
        // Arrange
        var userId1 = "user-1";
        var userId2 = "user-2";
        var rehearsal1 = new Rehearsal { Id = 1, Date = DateTime.UtcNow.AddDays(-1) };
        var rehearsal2 = new Rehearsal { Id = 2, Date = DateTime.UtcNow.AddDays(-2) };
        var event1 = Event.Create("Event 1", DateTime.UtcNow.AddDays(-1), "Location", EventType.Festival);
        event1.Id = 1;

        await _context.Rehearsals.AddRangeAsync(rehearsal1, rehearsal2);
        await _context.Events.AddAsync(event1);

        // User1: 2 rehearsals + 1 event
        var attendance1a = RehearsalAttendance.Create(1, userId1);
        attendance1a.Attended = true;
        var attendance1b = RehearsalAttendance.Create(2, userId1);
        attendance1b.Attended = true;
        var enrollment1 = Enrollment.Create(userId1, 1);
        enrollment1.WillAttend = true;

        // User2: 1 rehearsal only
        var attendance2 = RehearsalAttendance.Create(1, userId2);
        attendance2.Attended = true;

        await _context.RehearsalAttendances.AddRangeAsync(attendance1a, attendance1b, attendance2);
        await _context.Enrollments.AddAsync(enrollment1);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetRankProgressBatchAsync(new[] { userId1, userId2 });

        // Assert
        result.Should().HaveCount(2);
        result[userId1].CurrentXp.Should().Be(70); // 2 rehearsals (20) + 1 Festival (50) = 70
        result[userId2].CurrentXp.Should().Be(10); // 1 rehearsal (10) = 10
    }

    [Fact]
    public async Task GetRankProgressBatchAsync_WithDateRange_ExcludesFutureEvents()
    {
        // Arrange
        var userId = "user-123";
        var startDate = DateTime.UtcNow.AddDays(-10);
        var endDate = DateTime.UtcNow.AddDays(5); // End date in the future

        var pastEvent = Event.Create("Past Event", DateTime.UtcNow.AddDays(-5), "Location", EventType.Festival);
        pastEvent.Id = 1;
        var futureEvent = Event.Create("Future Event", DateTime.UtcNow.AddDays(3), "Location", EventType.Atuacao);
        futureEvent.Id = 2;

        await _context.Events.AddRangeAsync(pastEvent, futureEvent);
        var enrollment1 = Enrollment.Create(userId, 1);
        enrollment1.WillAttend = true;
        var enrollment2 = Enrollment.Create(userId, 2);
        enrollment2.WillAttend = true;
        await _context.Enrollments.AddRangeAsync(enrollment1, enrollment2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetRankProgressBatchAsync(new[] { userId }, startDate, endDate);

        // Assert - Only past event should count (future events are excluded even if within date range)
        result.Should().ContainKey(userId);
        result[userId].CurrentXp.Should().Be(50); // 1 Festival event * 50 XP
    }
}
