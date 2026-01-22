using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Repositories;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Application.Tests.Utilities;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

public class RankingServiceTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly RankingConfiguration _config;
    private readonly RankingService _service;

    public RankingServiceTests(DatabaseFixture fixture)
    {
        // Clean database at constructor start to ensure test isolation
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();

        _fixture = fixture;
        _context = _fixture.CreateContext();
        _mockUserManager = MockHelpers.CreateMockUserManager();

        // Setup test configuration
        _config = new RankingConfiguration
        {
            XpPerRehearsal = 10,
            XpPerEventType = new Dictionary<string, int>
            {
                { "Festival", 50 },
                { "Atuacao", 25 },
                { "Casamento", 30 }
            },
            Levels = new List<LevelDefinition>
            {
                new() { Level = 1, Name = "Lei Seca", XpThreshold = 0 },
                new() { Level = 2, Name = "Vai mais um copo", XpThreshold = 100 },
                new() { Level = 3, Name = "Já vai alto", XpThreshold = 250 },
                new() { Level = 4, Name = "Tá na brasa", XpThreshold = 500 },
                new() { Level = 5, Name = "Veterano das Cervejas", XpThreshold = 1000 }
            }
        };

        // Create mocks for new dependencies
        var mockPushNotificationFactory = new Mock<IPushNotificationFactory>();
        var mockPushNotificationService = new Mock<IPushNotificationService>();
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

        var configOptions = Options.Create(_config);
        var attendanceRepo = new RehearsalAttendanceRepository(_context);
        var enrollmentRepo = new EnrollmentRepository(_context);
        _service = new RankingService(
            attendanceRepo,
            enrollmentRepo,
            _mockUserManager.Object,
            configOptions,
            mockPushNotificationFactory.Object,
            mockPushNotificationService.Object,
            mockHttpContextAccessor.Object);
    }

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

    [Fact]
    public void GetLevelFromXp_WithZeroXp_ReturnsLevel1()
    {
        // Act
        var result = _service.GetLevelFromXp(0);

        // Assert
        result.Should().Be(1);
    }

    [Fact]
    public void GetLevelFromXp_WithXpForLevel2_ReturnsLevel2()
    {
        // Act
        var result = _service.GetLevelFromXp(100);

        // Assert
        result.Should().Be(2);
    }

    [Fact]
    public void GetLevelFromXp_WithXpBetweenLevels_ReturnsCurrentLevel()
    {
        // Act
        var result = _service.GetLevelFromXp(150); // Between level 2 (100) and level 3 (250)

        // Assert
        result.Should().Be(2);
    }

    [Fact]
    public void GetLevelFromXp_WithMaxXp_ReturnsMaxLevel()
    {
        // Act
        var result = _service.GetLevelFromXp(1500); // Above level 5 threshold

        // Assert
        result.Should().Be(5);
    }

    [Fact]
    public void GetRankName_WithValidLevel_ReturnsCorrectName()
    {
        // Act
        var result = _service.GetRankName(3);

        // Assert
        result.Should().Be("Já vai alto");
    }

    [Fact]
    public void GetRankName_WithInvalidLevel_ReturnsUnknown()
    {
        // Act
        var result = _service.GetRankName(999);

        // Assert
        result.Should().Be("Desconhecido");
    }

    [Fact]
    public void GetXpForNextLevel_WithLevelBelowMax_ReturnsNextThreshold()
    {
        // Act
        var result = _service.GetXpForNextLevel(2);

        // Assert
        result.Should().Be(250); // Level 3 threshold
    }

    [Fact]
    public void GetXpForNextLevel_WithMaxLevel_ReturnsMaxValue()
    {
        // Act
        var result = _service.GetXpForNextLevel(5);

        // Assert
        result.Should().Be(int.MaxValue);
    }

    [Fact]
    public void GetXpForCurrentLevel_WithValidLevel_ReturnsCorrectThreshold()
    {
        // Act
        var result = _service.GetXpForCurrentLevel(3);

        // Assert
        result.Should().Be(250);
    }

    [Fact]
    public async Task GetRankProgressAsync_WithValidUser_ReturnsCorrectProgress()
    {
        // Arrange
        var userId = "user-123";
        var user = new ApplicationUser { Id = userId, UserName = "testuser", Nickname = "Test" };
        var rehearsal = new Rehearsal { Id = 1, Date = DateTime.UtcNow.AddDays(-1) };

        _mockUserManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync(user);
        await _context.Rehearsals.AddAsync(rehearsal);
        var attendance = RehearsalAttendance.Create(1, userId);
        attendance.Attended = true;
        await _context.RehearsalAttendances.AddAsync(attendance);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetRankProgressAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.CurrentXp.Should().Be(10); // 1 rehearsal * 10 XP
        result.CurrentLevel.Should().Be(1);
        result.CurrentRankName.Should().Be("Lei Seca");
        result.XpForCurrentLevel.Should().Be(0);
        result.XpForNextLevel.Should().Be(100);
        result.XpToNextLevel.Should().Be(90);
        result.ProgressPercentage.Should().Be(10);
        result.IsMaxLevel.Should().BeFalse();
    }

    [Fact]
    public async Task GetRankProgressAsync_WithMaxLevel_ReturnsMaxLevelProgress()
    {
        // Arrange
        var userId = "user-123";
        var user = new ApplicationUser { Id = userId, UserName = "testuser", Nickname = "Test" };
        var rehearsal = new Rehearsal { Id = 1, Date = DateTime.UtcNow.AddDays(-1) };

        _mockUserManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync(user);
        await _context.Rehearsals.AddAsync(rehearsal);

        // Add enough attendances to reach max level (1000 XP)
        for (int i = 1; i <= 100; i++)
        {
            var attendance = RehearsalAttendance.Create(1, userId);
            attendance.Attended = true;
            await _context.RehearsalAttendances.AddAsync(attendance);
        }
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetRankProgressAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.CurrentXp.Should().Be(1000); // 100 rehearsals * 10 XP
        result.CurrentLevel.Should().Be(5);
        result.CurrentRankName.Should().Be("Veterano das Cervejas");
        result.IsMaxLevel.Should().BeTrue();
        result.XpToNextLevel.Should().Be(0);
        result.ProgressPercentage.Should().Be(100);
    }

    [Fact]
    public async Task UpdateUserRankingAsync_UpdatesUserXpAndLevel()
    {
        // Arrange
        var userId = "user-123";
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "testuser",
            Nickname = "Test",
            ExperiencePoints = 0,
            Level = 1
        };
        var rehearsal = new Rehearsal { Id = 1, Date = DateTime.UtcNow.AddDays(-1) };

        _mockUserManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        await _context.Rehearsals.AddAsync(rehearsal);
        var attendance = RehearsalAttendance.Create(1, userId);
        attendance.Attended = true;
        await _context.RehearsalAttendances.AddAsync(attendance);
        await _context.SaveChangesAsync();

        // Act
        await _service.UpdateUserRankingAsync(userId);

        // Assert
        user.ExperiencePoints.Should().Be(10);
        user.Level.Should().Be(1);
        _mockUserManager.Verify(x => x.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task CalculateTotalXpAsync_WithEventTypeSpecificXp_UseConfiguredValue()
    {
        // Arrange
        var userId = "user-123";
        var festivalEvent = Event.Create("Festival Event", DateTime.UtcNow.AddDays(-1), "Location", EventType.Festival);
        festivalEvent.Id = 1;

        await _context.Events.AddAsync(festivalEvent);
        var enrollment = Enrollment.Create(userId, 1);
        enrollment.WillAttend = true;
        await _context.Enrollments.AddAsync(enrollment);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CalculateTotalXpAsync(userId);

        // Assert - Festival gives 50 XP according to config
        result.Should().Be(50);
    }

    [Fact]
    public async Task CalculateTotalXpAsync_WithEventTypeNotInConfig_GivesZeroXp()
    {
        // Arrange
        var userId = "user-123";
        var nebraEvent = Event.Create("Nerba Event", DateTime.UtcNow.AddDays(-1), "Location", EventType.Nerba);
        nebraEvent.Id = 1;

        await _context.Events.AddAsync(nebraEvent);
        var enrollment = Enrollment.Create(userId, 1);
        enrollment.WillAttend = true;
        await _context.Enrollments.AddAsync(enrollment);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CalculateTotalXpAsync(userId);

        // Assert - Nerba not in config, gives 0 XP
        result.Should().Be(0);
    }

    [Fact]
    public async Task CalculateTotalXpAsync_WithMixedEventTypes_CalculatesCorrectTotal()
    {
        // Arrange
        var userId = "user-123";
        var festivalEvent = Event.Create("Festival", DateTime.UtcNow.AddDays(-1), "Location", EventType.Festival);
        festivalEvent.Id = 1;
        var atuacaoEvent = Event.Create("Atuacao", DateTime.UtcNow.AddDays(-2), "Location", EventType.Atuacao);
        atuacaoEvent.Id = 2;
        var casamentoEvent = Event.Create("Casamento", DateTime.UtcNow.AddDays(-3), "Location", EventType.Casamento);
        casamentoEvent.Id = 3;

        await _context.Events.AddRangeAsync(festivalEvent, atuacaoEvent, casamentoEvent);
        var enrollment1 = Enrollment.Create(userId, 1);
        enrollment1.WillAttend = true;
        var enrollment2 = Enrollment.Create(userId, 2);
        enrollment2.WillAttend = true;
        var enrollment3 = Enrollment.Create(userId, 3);
        enrollment3.WillAttend = true;
        await _context.Enrollments.AddRangeAsync(enrollment1, enrollment2, enrollment3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CalculateTotalXpAsync(userId);

        // Assert - Festival (50) + Atuacao (25) + Casamento (30) = 105
        result.Should().Be(105);
    }

    [Fact]
    public void GetLevelFromXp_WithExactThreshold_ReturnsCorrectLevel()
    {
        // Arrange - Test exact threshold values
        // Level thresholds: 1=0, 2=100, 3=250, 4=500, 5=1000

        // Act & Assert - Test each exact threshold
        _service.GetLevelFromXp(0).Should().Be(1);     // Exact threshold for level 1
        _service.GetLevelFromXp(100).Should().Be(2);   // Exact threshold for level 2
        _service.GetLevelFromXp(250).Should().Be(3);   // Exact threshold for level 3
        _service.GetLevelFromXp(500).Should().Be(4);   // Exact threshold for level 4
        _service.GetLevelFromXp(1000).Should().Be(5);  // Exact threshold for level 5
    }

    [Fact]
    public void GetLevelFromXp_WithOneXpBelowThreshold_ReturnsLowerLevel()
    {
        // Arrange - Test values one below threshold
        // Level thresholds: 1=0, 2=100, 3=250, 4=500, 5=1000

        // Act & Assert
        _service.GetLevelFromXp(99).Should().Be(1);    // Just below level 2 threshold
        _service.GetLevelFromXp(249).Should().Be(2);   // Just below level 3 threshold
        _service.GetLevelFromXp(499).Should().Be(3);   // Just below level 4 threshold
        _service.GetLevelFromXp(999).Should().Be(4);   // Just below level 5 threshold
    }

    [Fact]
    public void GetLevelFromXp_WithOneXpAboveThreshold_ReturnsCorrectLevel()
    {
        // Arrange - Test values one above threshold
        // Level thresholds: 1=0, 2=100, 3=250, 4=500, 5=1000

        // Act & Assert
        _service.GetLevelFromXp(1).Should().Be(1);     // Just above level 1 threshold
        _service.GetLevelFromXp(101).Should().Be(2);   // Just above level 2 threshold
        _service.GetLevelFromXp(251).Should().Be(3);   // Just above level 3 threshold
        _service.GetLevelFromXp(501).Should().Be(4);   // Just above level 4 threshold
        _service.GetLevelFromXp(1001).Should().Be(5);  // Just above level 5 threshold
    }

    [Fact]
    public void GetLevelFromXp_WithNegativeXp_ReturnsLowestLevel()
    {
        // Arrange - Edge case with negative XP

        // Act
        var result = _service.GetLevelFromXp(-100);

        // Assert - Should return lowest level (1)
        result.Should().Be(1);
    }

    [Fact]
    public void GetLevelFromXp_WithVeryLargeXp_ReturnsMaxLevel()
    {
        // Arrange - Test very large XP values

        // Act
        var result = _service.GetLevelFromXp(int.MaxValue - 1);

        // Assert - Should return max level (5)
        result.Should().Be(5);
    }

    public void Dispose()
    {
        _fixture.CleanDatabase(_context).GetAwaiter().GetResult();
        _context.Dispose();
    }
}
