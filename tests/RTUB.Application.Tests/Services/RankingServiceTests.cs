using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Moq;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace RTUB.Application.Tests.Services;

public class RankingServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly RankingConfiguration _config;
    private readonly RankingService _service;

    public RankingServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options, Mock.Of<Microsoft.AspNetCore.Http.IHttpContextAccessor>(), new AuditContext());
        
        // Mock UserManager
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        _mockUserManager = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null, null, null, null, null, null, null, null);
        
        // Setup test configuration
        _config = new RankingConfiguration
        {
            Enabled = true,
            XpPerRehearsal = 10,
            XpPerEvent = 20,
            Levels = new List<LevelDefinition>
            {
                new() { Level = 1, Name = "Lei Seca", XpThreshold = 0 },
                new() { Level = 2, Name = "Vai mais um copo", XpThreshold = 100 },
                new() { Level = 3, Name = "Já vai alto", XpThreshold = 250 },
                new() { Level = 4, Name = "Tá na brasa", XpThreshold = 500 },
                new() { Level = 5, Name = "Veterano das Cervejas", XpThreshold = 1000 }
            }
        };
        
        var configOptions = Options.Create(_config);
        _service = new RankingService(_context, _mockUserManager.Object, configOptions);
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
        var rehearsal1 = new Rehearsal { Id = 1, Date = DateTime.Now };
        var rehearsal2 = new Rehearsal { Id = 2, Date = DateTime.Now };
        
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
        var event1 = new Event { Id = 1, Name = "Event 1", Date = DateTime.Now };
        var event2 = new Event { Id = 2, Name = "Event 2", Date = DateTime.Now };
        
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

        // Assert - 2 events * 20 XP = 40
        result.Should().Be(40);
    }

    [Fact]
    public async Task CalculateTotalXpAsync_WithMixedAttendance_CalculatesCorrectly()
    {
        // Arrange
        var userId = "user-123";
        var rehearsal = new Rehearsal { Id = 1, Date = DateTime.Now };
        var eventEntity = new Event { Id = 1, Name = "Event 1", Date = DateTime.Now };
        
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

        // Assert - 1 rehearsal (10 XP) + 1 event (20 XP) = 30
        result.Should().Be(30);
    }

    [Fact]
    public async Task CalculateTotalXpAsync_OnlyCountsAttendedRehearsals()
    {
        // Arrange
        var userId = "user-123";
        var rehearsal1 = new Rehearsal { Id = 1, Date = DateTime.Now };
        var rehearsal2 = new Rehearsal { Id = 2, Date = DateTime.Now };
        
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
        var event1 = new Event { Id = 1, Name = "Event 1", Date = DateTime.Now };
        var event2 = new Event { Id = 2, Name = "Event 2", Date = DateTime.Now };
        
        await _context.Events.AddRangeAsync(event1, event2);
        var enrollment1 = Enrollment.Create(userId, 1);
        enrollment1.WillAttend = true;
        var enrollment2 = Enrollment.Create(userId, 2);
        enrollment2.WillAttend = false;
        await _context.Enrollments.AddRangeAsync(enrollment1, enrollment2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CalculateTotalXpAsync(userId);

        // Assert - Only 1 confirmed enrollment * 20 XP = 20
        result.Should().Be(20);
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
    public async Task IsRankingVisibleAsync_WhenEnabled_ReturnsTrue()
    {
        // Arrange
        var userId = "user-123";

        // Act
        var result = await _service.IsRankingVisibleAsync(userId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsRankingVisibleAsync_WhenDisabledButUserIsOwner_ReturnsTrue()
    {
        // Arrange
        var userId = "user-123";
        var user = new ApplicationUser { Id = userId, UserName = "owner", Nickname = "Owner" };
        
        _config.Enabled = false;
        _mockUserManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.IsInRoleAsync(user, "Owner")).ReturnsAsync(true);

        // Act
        var result = await _service.IsRankingVisibleAsync(userId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsRankingVisibleAsync_WhenDisabledAndNotOwner_ReturnsFalse()
    {
        // Arrange
        var userId = "user-123";
        var user = new ApplicationUser { Id = userId, UserName = "member", Nickname = "Member" };
        
        _config.Enabled = false;
        _mockUserManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.IsInRoleAsync(user, "Owner")).ReturnsAsync(false);

        // Act
        var result = await _service.IsRankingVisibleAsync(userId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetRankProgressAsync_WithValidUser_ReturnsCorrectProgress()
    {
        // Arrange
        var userId = "user-123";
        var user = new ApplicationUser { Id = userId, UserName = "testuser", Nickname = "Test" };
        var rehearsal = new Rehearsal { Id = 1, Date = DateTime.Now };
        
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
        var rehearsal = new Rehearsal { Id = 1, Date = DateTime.Now };
        
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
        var rehearsal = new Rehearsal { Id = 1, Date = DateTime.Now };
        
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

    public void Dispose()
    {
        _context.Dispose();
    }
}
