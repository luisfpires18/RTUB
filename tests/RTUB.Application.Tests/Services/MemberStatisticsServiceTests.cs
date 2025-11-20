using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for MemberStatisticsService
/// Tests business logic for member statistics and aggregated data queries
/// </summary>
public class MemberStatisticsServiceTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly MemberStatisticsService _service;

    public MemberStatisticsServiceTests(DatabaseFixture fixture)
    {
        // Clean database at constructor start to ensure test isolation
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();

        _context = _fixture.CreateContext();
        _service = new MemberStatisticsService(_context);
    }

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

    [Fact]
    public async Task GetEnrollmentsByUserWithEventTypeAsync_WithNoEnrollments_ReturnsEmptyList()
    {
        // Arrange
        var beforeDate = DateTime.UtcNow;

        // Act
        var result = await _service.GetEnrollmentsByUserWithEventTypeAsync(beforeDate);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEnrollmentsByUserWithEventTypeAsync_WithValidEnrollments_ReturnsCorrectData()
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

        var event1 = Event.Create("Event 1", pastDate1, "Location 1", EventType.Festival);
        var event2 = Event.Create("Event 2", pastDate2, "Location 2", EventType.Atuacao);
        var event3 = Event.Create("Event 3", futureDate, "Location 3", EventType.Casamento);

        _context.Events.AddRange(event1, event2, event3);
        await _context.SaveChangesAsync();

        // User1: 2 past events with WillAttend = true
        var enrollment1 = Enrollment.Create(user1.Id, event1.Id);
        enrollment1.WillAttend = true;

        var enrollment2 = Enrollment.Create(user1.Id, event2.Id);
        enrollment2.WillAttend = true;

        // User2: 1 past event with WillAttend = true
        var enrollment3 = Enrollment.Create(user2.Id, event1.Id);
        enrollment3.WillAttend = true;

        // User2: 1 past event with WillAttend = false (should not be counted)
        var enrollment4 = Enrollment.Create(user2.Id, event2.Id);
        enrollment4.WillAttend = false;

        // User1: 1 future event (should not be counted)
        var enrollment5 = Enrollment.Create(user1.Id, event3.Id);
        enrollment5.WillAttend = true;

        _context.Enrollments.AddRange(enrollment1, enrollment2, enrollment3, enrollment4, enrollment5);
        await _context.SaveChangesAsync();

        var beforeDate = DateTime.UtcNow;

        // Act
        var result = await _service.GetEnrollmentsByUserWithEventTypeAsync(beforeDate);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3); // 2 from user1 + 1 from user2

        var user1Enrollments = result.Where(e => e.UserId == user1.Id).ToList();
        user1Enrollments.Should().HaveCount(2);
        user1Enrollments.Should().Contain(e => e.EventType == EventType.Festival);
        user1Enrollments.Should().Contain(e => e.EventType == EventType.Atuacao);

        var user2Enrollments = result.Where(e => e.UserId == user2.Id).ToList();
        user2Enrollments.Should().HaveCount(1);
        user2Enrollments.Should().Contain(e => e.EventType == EventType.Festival);
    }

    [Fact]
    public async Task GetEnrollmentsByUserWithEventTypeAsync_OnlyIncludesWillAttendEnrollments()
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
        var event1 = Event.Create("Event 1", pastDate, "Location", EventType.Festival);
        _context.Events.Add(event1);
        await _context.SaveChangesAsync();

        // Create enrollment with WillAttend = false
        var enrollment = Enrollment.Create(user.Id, event1.Id);
        enrollment.WillAttend = false;
        _context.Enrollments.Add(enrollment);
        await _context.SaveChangesAsync();

        var beforeDate = DateTime.UtcNow;

        // Act
        var result = await _service.GetEnrollmentsByUserWithEventTypeAsync(beforeDate);

        // Assert
        result.Should().BeEmpty(); // WillAttend = false, so not included
    }

    [Fact]
    public async Task GetEnrollmentsByUserWithEventTypeAsync_OnlyIncludesPastEvents()
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

        var futureDate = DateTime.UtcNow.AddDays(5);
        var event1 = Event.Create("Event 1", futureDate, "Location", EventType.Festival);
        _context.Events.Add(event1);
        await _context.SaveChangesAsync();

        var enrollment = Enrollment.Create(user.Id, event1.Id);
        enrollment.WillAttend = true;
        _context.Enrollments.Add(enrollment);
        await _context.SaveChangesAsync();

        var beforeDate = DateTime.UtcNow;

        // Act
        var result = await _service.GetEnrollmentsByUserWithEventTypeAsync(beforeDate);

        // Assert
        result.Should().BeEmpty(); // Future event, so not included
    }

    [Fact]
    public async Task Constructor_WithNullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new MemberStatisticsService(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("context");
    }

    public void Dispose()
    {
        _context?.Dispose();
        GC.SuppressFinalize(this);
    }
}
