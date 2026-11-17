// RankingService: levels.
using FluentAssertions;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Repositories;
using RTUB.Application.Services;

namespace RTUB.Application.Tests.Services;

public partial class RankingServiceTests
{
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

    [Fact]
    public void GetXpForCurrentLevel_WithInvalidLevel_ReturnsZero()
    {
        // Act
        var result = _service.GetXpForCurrentLevel(999);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void GetLevelFromXp_WithEmptyLevelsConfig_ReturnsLevel1()
    {
        // Arrange
        var emptyConfig = new RankingConfiguration
        {
            XpPerRehearsal = 10,
            XpPerEventType = new Dictionary<string, int>(),
            Levels = new List<LevelDefinition>() // Empty levels
        };


        var configOptions = Options.Create(emptyConfig);
        var attendanceRepo = new RehearsalAttendanceRepository(_fixture.CreateContextFactory());
        var enrollmentRepo = new EnrollmentRepository(_fixture.CreateContextFactory());
        var serviceWithEmptyConfig = new RankingService(
            attendanceRepo,
            enrollmentRepo,
            configOptions);

        // Act
        var result = serviceWithEmptyConfig.GetLevelFromXp(1000);

        // Assert
        result.Should().Be(1);
    }

    [Fact]
    public void GetLevelFromXp_WithNullLevelsConfig_ReturnsLevel1()
    {
        // Arrange
        var nullConfig = new RankingConfiguration
        {
            XpPerRehearsal = 10,
            XpPerEventType = new Dictionary<string, int>(),
            Levels = null! // Null levels
        };


        var configOptions = Options.Create(nullConfig);
        var attendanceRepo = new RehearsalAttendanceRepository(_fixture.CreateContextFactory());
        var enrollmentRepo = new EnrollmentRepository(_fixture.CreateContextFactory());
        var serviceWithNullConfig = new RankingService(
            attendanceRepo,
            enrollmentRepo,
            configOptions);

        // Act
        var result = serviceWithNullConfig.GetLevelFromXp(1000);

        // Assert
        result.Should().Be(1);
    }
}
