// MemberStatisticsService: attendance counts.
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

public partial class MemberStatisticsServiceTests
{
    [Fact]
    public async Task GetRehearsalAttendanceCountsByUserAsync_WithNoAttendances_ReturnsEmptyDictionary()
    {
        // Arrange
        var beforeDate = DateTime.UtcNow;

        // Act
        var result = await _service.GetRehearsalAttendanceCountsByUserAsync(beforeDate);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRehearsalAttendanceCountsByUserAsync_WithAttendedRehearsals_ReturnsCorrectCounts()
    {
        // Arrange
        var userId1 = Guid.NewGuid().ToString();
        var userId2 = Guid.NewGuid().ToString();

        var user1 = new ApplicationUser
        {
            Id = userId1,
            UserName = $"user1_{userId1}@test.com",
            Email = $"user1_{userId1}@test.com",
            FirstName = "Test",
            LastName = "User1",
            Nickname = "User1",
            PhoneNumber = "123456789"
        };
        var user2 = new ApplicationUser
        {
            Id = userId2,
            UserName = $"user2_{userId2}@test.com",
            Email = $"user2_{userId2}@test.com",
            FirstName = "Test",
            LastName = "User2",
            Nickname = "User2",
            PhoneNumber = "987654321"
        };

        _context.Users.AddRange(user1, user2);
        await _context.SaveChangesAsync();

        var pastDate1 = DateTime.UtcNow.AddDays(-10);
        var pastDate2 = DateTime.UtcNow.AddDays(-5);
        var futureDate = DateTime.UtcNow.AddDays(5);

        var rehearsal1 = Rehearsal.Create(pastDate1, "Location 1");
        var rehearsal2 = Rehearsal.Create(pastDate2, "Location 2");
        var rehearsal3 = Rehearsal.Create(futureDate, "Location 3");

        _context.Rehearsals.AddRange(rehearsal1, rehearsal2, rehearsal3);
        await _context.SaveChangesAsync();

        // User1: 2 attended past rehearsals
        var attendance1 = RehearsalAttendance.Create(rehearsal1.Id, user1.Id);
        attendance1.MarkAttendance(true);

        var attendance2 = RehearsalAttendance.Create(rehearsal2.Id, user1.Id);
        attendance2.MarkAttendance(true);

        // User2: 1 attended past rehearsal
        var attendance3 = RehearsalAttendance.Create(rehearsal1.Id, user2.Id);
        attendance3.MarkAttendance(true);

        // User2: 1 not attended (should not be counted)
        var attendance4 = RehearsalAttendance.Create(rehearsal2.Id, user2.Id);
        attendance4.MarkAttendance(false);

        // User1: 1 future rehearsal (should not be counted)
        var attendance5 = RehearsalAttendance.Create(rehearsal3.Id, user1.Id);
        attendance5.MarkAttendance(true);

        _context.RehearsalAttendances.AddRange(attendance1, attendance2, attendance3, attendance4, attendance5);
        await _context.SaveChangesAsync();

        var beforeDate = DateTime.UtcNow;

        // Act
        var result = await _service.GetRehearsalAttendanceCountsByUserAsync(beforeDate);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result[user1.Id].Should().Be(2); // User1 attended 2 past rehearsals
        result[user2.Id].Should().Be(1); // User2 attended 1 past rehearsal
    }

    [Fact]
    public async Task GetRehearsalAttendanceCountsByUserAsync_OnlyCountsAttendedRehearsals()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = $"user_{userId}@test.com",
            Email = $"user_{userId}@test.com",
            FirstName = "Test",
            LastName = "User",
            Nickname = "User",
            PhoneNumber = "123456789"
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var pastDate = DateTime.UtcNow.AddDays(-5);
        var rehearsal = Rehearsal.Create(pastDate, "Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        // Create attendance with Attended = false
        var attendance = RehearsalAttendance.Create(rehearsal.Id, user.Id);
        attendance.MarkAttendance(false);
        _context.RehearsalAttendances.Add(attendance);
        await _context.SaveChangesAsync();

        var beforeDate = DateTime.UtcNow;

        // Act
        var result = await _service.GetRehearsalAttendanceCountsByUserAsync(beforeDate);

        // Assert
        result.Should().BeEmpty(); // Not attended, so not counted
    }
}
