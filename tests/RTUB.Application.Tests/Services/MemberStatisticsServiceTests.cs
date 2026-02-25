using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
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

    // Test XP configuration constants
    private const int TestXpPerRehearsal = 12;
    private const int TestXpForFestival = 80;
    private const int TestXpForAtuacao = 30;
    private const int TestXpForConvivio = 50;

    public MemberStatisticsServiceTests(DatabaseFixture fixture)
    {
        // Clean database at constructor start to ensure test isolation
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();

        _context = _fixture.CreateContext();

        // Create mock XpSettings for tests using constants
        var xpSettings = Options.Create(new XpSettings
        {
            XpPerRehearsal = TestXpPerRehearsal,
            XpPerEventType = new Dictionary<string, int>
            {
                { "Festival", TestXpForFestival },
                { "Atuacao", TestXpForAtuacao },
                { "Convivio", TestXpForConvivio }
            }
        });

        _service = new MemberStatisticsService(_fixture.CreateContextFactory(), xpSettings);
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
    public async Task GetEnrollmentsByUserWithEventTypeAsync_ExcludesOngoingMultiDayEvents()
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

        var today = DateTime.UtcNow.Date;
        var event1 = Event.Create("Event 1", today, "Location", EventType.Convivio);
        event1.SetEndDate(today.AddDays(2)); // Multi-day event currently in progress
        _context.Events.Add(event1);
        await _context.SaveChangesAsync();

        var enrollment = Enrollment.Create(user.Id, event1.Id);
        enrollment.WillAttend = true;
        _context.Enrollments.Add(enrollment);
        await _context.SaveChangesAsync();

        var beforeDate = today.AddDays(1); // During the event window

        // Act
        var result = await _service.GetEnrollmentsByUserWithEventTypeAsync(beforeDate);

        // Assert
        result.Should().BeEmpty(); // Ongoing event should not be counted yet
    }

    [Fact]
    public async Task Constructor_WithNullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var xpSettings = Options.Create(new XpSettings());

        // Act & Assert
        var act = () => new MemberStatisticsService(null!, xpSettings);
        act.Should().Throw<ArgumentNullException>().WithParameterName("context");
    }

    [Fact]
    public async Task Constructor_WithNullXpSettings_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new MemberStatisticsService(_fixture.CreateContextFactory(), null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("xpSettings");
    }

    [Fact]
    public async Task GetUserXpBreakdownAsync_WithNoActivities_ReturnsZeroXp()
    {
        // Arrange
        var userId = "user-no-activities";
        var beforeDate = DateTime.UtcNow;

        // Act
        var result = await _service.GetUserXpBreakdownAsync(userId, beforeDate);

        // Assert
        result.Should().NotBeNull();
        result.TotalXp.Should().Be(0);
        result.RehearsalCount.Should().Be(0);
        result.RehearsalXpTotal.Should().Be(0);
        result.EventsByType.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserXpBreakdownAsync_WithRehearsalsAndEvents_CalculatesCorrectly()
    {
        // Arrange
        var userId = "user-with-activities";
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Nickname = "Tester"
        };
        await _context.Users.AddAsync(user);

        // Create 2 past rehearsals
        var rehearsal1 = new Rehearsal { Id = 1, Date = DateTime.UtcNow.AddDays(-5) };
        var rehearsal2 = new Rehearsal { Id = 2, Date = DateTime.UtcNow.AddDays(-3) };
        await _context.Rehearsals.AddRangeAsync(rehearsal1, rehearsal2);

        var attendance1 = RehearsalAttendance.Create(1, userId);
        attendance1.Attended = true;
        var attendance2 = RehearsalAttendance.Create(2, userId);
        attendance2.Attended = true;
        await _context.RehearsalAttendances.AddRangeAsync(attendance1, attendance2);

        // Create events of different types
        var festivalEvent = Event.Create("Festival", DateTime.UtcNow.AddDays(-4), "Location", EventType.Festival);
        festivalEvent.Id = 1;
        var atuacaoEvent1 = Event.Create("Atuacao 1", DateTime.UtcNow.AddDays(-2), "Location", EventType.Atuacao);
        atuacaoEvent1.Id = 2;
        var atuacaoEvent2 = Event.Create("Atuacao 2", DateTime.UtcNow.AddDays(-1), "Location", EventType.Atuacao);
        atuacaoEvent2.Id = 3;
        await _context.Events.AddRangeAsync(festivalEvent, atuacaoEvent1, atuacaoEvent2);

        var enrollment1 = Enrollment.Create(userId, 1);
        enrollment1.WillAttend = true;
        var enrollment2 = Enrollment.Create(userId, 2);
        enrollment2.WillAttend = true;
        var enrollment3 = Enrollment.Create(userId, 3);
        enrollment3.WillAttend = true;
        await _context.Enrollments.AddRangeAsync(enrollment1, enrollment2, enrollment3);

        await _context.SaveChangesAsync();

        var beforeDate = DateTime.UtcNow;

        // Act
        var result = await _service.GetUserXpBreakdownAsync(userId, beforeDate);

        // Assert
        result.Should().NotBeNull();
        result.RehearsalCount.Should().Be(2);
        result.RehearsalXpPerUnit.Should().Be(TestXpPerRehearsal);
        result.RehearsalXpTotal.Should().Be(2 * TestXpPerRehearsal);

        result.EventsByType.Should().HaveCount(2);

        var festivalXp = result.EventsByType.FirstOrDefault(e => e.TypeName == "Festival");
        festivalXp.Should().NotBeNull();
        festivalXp!.Count.Should().Be(1);
        festivalXp.XpPerUnit.Should().Be(TestXpForFestival);
        festivalXp.TotalXp.Should().Be(TestXpForFestival);

        var atuacaoXp = result.EventsByType.FirstOrDefault(e => e.TypeName == "Atuacao");
        atuacaoXp.Should().NotBeNull();
        atuacaoXp!.Count.Should().Be(2);
        atuacaoXp.XpPerUnit.Should().Be(TestXpForAtuacao);
        atuacaoXp.TotalXp.Should().Be(2 * TestXpForAtuacao);

        result.TotalXp.Should().Be((2 * TestXpPerRehearsal) + TestXpForFestival + (2 * TestXpForAtuacao));
    }

    [Fact]
    public async Task GetUserAttendedActivitiesAsync_WithNoActivities_ReturnsEmptyList()
    {
        // Arrange
        var userId = "user-no-activities";
        var beforeDate = DateTime.UtcNow;

        // Act
        var result = await _service.GetUserAttendedActivitiesAsync(userId, beforeDate);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserAttendedActivitiesAsync_WithActivities_ReturnsOrderedList()
    {
        // Arrange
        var userId = "user-with-activities-2";
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "testuser2",
            Email = "test2@example.com",
            FirstName = "Test",
            LastName = "User",
            Nickname = "Tester2"
        };
        await _context.Users.AddAsync(user);

        // Create rehearsals
        var rehearsal1 = new Rehearsal { Id = 10, Date = DateTime.UtcNow.AddDays(-5), Theme = "Fado Practice" };
        var rehearsal2 = new Rehearsal { Id = 11, Date = DateTime.UtcNow.AddDays(-1) };
        await _context.Rehearsals.AddRangeAsync(rehearsal1, rehearsal2);

        var attendance1 = RehearsalAttendance.Create(10, userId);
        attendance1.Attended = true;
        var attendance2 = RehearsalAttendance.Create(11, userId);
        attendance2.Attended = true;
        await _context.RehearsalAttendances.AddRangeAsync(attendance1, attendance2);

        // Create event
        var festivalEvent = Event.Create("Summer Festival", DateTime.UtcNow.AddDays(-3), "Location", EventType.Festival);
        festivalEvent.Id = 10;
        await _context.Events.AddAsync(festivalEvent);

        var enrollment = Enrollment.Create(userId, 10);
        enrollment.WillAttend = true;
        await _context.Enrollments.AddAsync(enrollment);

        await _context.SaveChangesAsync();

        var beforeDate = DateTime.UtcNow;

        // Act
        var result = await _service.GetUserAttendedActivitiesAsync(userId, beforeDate);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);

        // Should be ordered by date descending (newest first)
        result[0].Date.Should().BeCloseTo(DateTime.UtcNow.AddDays(-1), TimeSpan.FromSeconds(1));
        result[0].Name.Should().Be("Ensaio");
        result[0].Type.Should().Be("Ensaio");
        result[0].IsRehearsal.Should().BeTrue();
        result[0].XpEarned.Should().Be(TestXpPerRehearsal);

        result[1].Date.Should().BeCloseTo(DateTime.UtcNow.AddDays(-3), TimeSpan.FromSeconds(1));
        result[1].Name.Should().Be("Summer Festival");
        result[1].Type.Should().NotBeEmpty();
        result[1].IsRehearsal.Should().BeFalse();
        result[1].XpEarned.Should().Be(TestXpForFestival);

        result[2].Date.Should().BeCloseTo(DateTime.UtcNow.AddDays(-5), TimeSpan.FromSeconds(1));
        result[2].Name.Should().Be("Ensaio - Fado Practice");
        result[2].Type.Should().Be("Ensaio");
        result[2].IsRehearsal.Should().BeTrue();
        result[2].XpEarned.Should().Be(TestXpPerRehearsal);
    }

    [Fact]
    public async Task GetUserXpBreakdownAsync_OnlyCountsPastActivities()
    {
        // Arrange
        var userId = "user-with-future-activities";
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "testuser3",
            Email = "test3@example.com",
            FirstName = "Test",
            LastName = "User",
            Nickname = "Tester3"
        };
        await _context.Users.AddAsync(user);

        // Create past and future rehearsals
        var pastRehearsal = new Rehearsal { Id = 20, Date = DateTime.UtcNow.AddDays(-2) };
        var futureRehearsal = new Rehearsal { Id = 21, Date = DateTime.UtcNow.AddDays(2) };
        await _context.Rehearsals.AddRangeAsync(pastRehearsal, futureRehearsal);

        var pastAttendance = RehearsalAttendance.Create(20, userId);
        pastAttendance.Attended = true;
        var futureAttendance = RehearsalAttendance.Create(21, userId);
        futureAttendance.Attended = true;
        await _context.RehearsalAttendances.AddRangeAsync(pastAttendance, futureAttendance);

        await _context.SaveChangesAsync();

        var beforeDate = DateTime.UtcNow;

        // Act
        var result = await _service.GetUserXpBreakdownAsync(userId, beforeDate);

        // Assert
        result.Should().NotBeNull();
        result.RehearsalCount.Should().Be(1); // Only past rehearsal
        result.RehearsalXpTotal.Should().Be(TestXpPerRehearsal);
        result.TotalXp.Should().Be(TestXpPerRehearsal);
    }

    public void Dispose()
    {
        _context?.Dispose();
        GC.SuppressFinalize(this);
    }
}
