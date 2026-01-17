using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Core.Tests.Entities;

public class GameScoreTests
{
    #region Create Tests

    [Fact]
    public void Create_WithValidData_ShouldCreateInstance()
    {
        // Arrange
        var userId = "user-123";
        var gameKey = "test-game";
        var points = 100;
        var maxLevel = 5;
        var timeSurvived = TimeSpan.FromMinutes(3);

        // Act
        var score = GameScore.Create(userId, gameKey, points, maxLevel, timeSurvived);

        // Assert
        score.Should().NotBeNull();
        score.UserId.Should().Be(userId);
        score.GameKey.Should().Be(gameKey);
        score.Points.Should().Be(points);
        score.MaxLevel.Should().Be(maxLevel);
        score.TimeSurvived.Should().Be(timeSurvived);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_WithEmptyUserId_ShouldThrowException(string? userId)
    {
        // Act
        var act = () => GameScore.Create(userId!, "game-key", 100, 5, TimeSpan.FromMinutes(1));

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*User ID*");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_WithEmptyGameKey_ShouldThrowException(string? gameKey)
    {
        // Act
        var act = () => GameScore.Create("user-id", gameKey!, 100, 5, TimeSpan.FromMinutes(1));

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Game key*");
    }

    [Fact]
    public void Create_WithNegativePoints_ShouldThrowException()
    {
        // Act
        var act = () => GameScore.Create("user-id", "game-key", -1, 5, TimeSpan.FromMinutes(1));

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Points*");
    }

    [Fact]
    public void Create_WithNegativeMaxLevel_ShouldThrowException()
    {
        // Act
        var act = () => GameScore.Create("user-id", "game-key", 100, -1, TimeSpan.FromMinutes(1));

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Max level*");
    }

    #endregion

    #region UpdateIfBetter Tests

    [Fact]
    public void UpdateIfBetter_WithHigherPoints_ShouldUpdateAndReturnTrue()
    {
        // Arrange
        var score = GameScore.Create("user-id", "game-key", 50, 3, TimeSpan.FromMinutes(1));

        // Act
        var result = score.UpdateIfBetter(100, 5, TimeSpan.FromMinutes(2));

        // Assert
        result.Should().BeTrue();
        score.Points.Should().Be(100);
        score.MaxLevel.Should().Be(5);
        score.TimeSurvived.Should().Be(TimeSpan.FromMinutes(2));
    }

    [Fact]
    public void UpdateIfBetter_WithSamePointsAndHigherLevel_ShouldUpdateAndReturnTrue()
    {
        // Arrange
        var score = GameScore.Create("user-id", "game-key", 100, 3, TimeSpan.FromMinutes(1));

        // Act
        var result = score.UpdateIfBetter(100, 7, TimeSpan.FromMinutes(2));

        // Assert
        result.Should().BeTrue();
        score.Points.Should().Be(100);
        score.MaxLevel.Should().Be(7);
        score.TimeSurvived.Should().Be(TimeSpan.FromMinutes(2));
    }

    [Fact]
    public void UpdateIfBetter_WithLowerPoints_ShouldNotUpdateAndReturnFalse()
    {
        // Arrange
        var score = GameScore.Create("user-id", "game-key", 100, 5, TimeSpan.FromMinutes(2));

        // Act
        var result = score.UpdateIfBetter(50, 3, TimeSpan.FromMinutes(1));

        // Assert
        result.Should().BeFalse();
        score.Points.Should().Be(100);
        score.MaxLevel.Should().Be(5);
        score.TimeSurvived.Should().Be(TimeSpan.FromMinutes(2));
    }

    [Fact]
    public void UpdateIfBetter_WithSamePointsAndLowerLevel_ShouldNotUpdateAndReturnFalse()
    {
        // Arrange
        var score = GameScore.Create("user-id", "game-key", 100, 7, TimeSpan.FromMinutes(2));

        // Act
        var result = score.UpdateIfBetter(100, 3, TimeSpan.FromMinutes(1));

        // Assert
        result.Should().BeFalse();
        score.Points.Should().Be(100);
        score.MaxLevel.Should().Be(7);
        score.TimeSurvived.Should().Be(TimeSpan.FromMinutes(2));
    }

    [Fact]
    public void UpdateIfBetter_WithSamePointsAndSameLevel_ShouldNotUpdateAndReturnFalse()
    {
        // Arrange
        var score = GameScore.Create("user-id", "game-key", 100, 5, TimeSpan.FromMinutes(2));

        // Act
        var result = score.UpdateIfBetter(100, 5, TimeSpan.FromMinutes(3));

        // Assert
        result.Should().BeFalse();
        score.Points.Should().Be(100);
        score.MaxLevel.Should().Be(5);
        score.TimeSurvived.Should().Be(TimeSpan.FromMinutes(2));
    }

    [Fact]
    public void UpdateIfBetter_WithNegativePoints_ShouldThrowException()
    {
        // Arrange
        var score = GameScore.Create("user-id", "game-key", 100, 5, TimeSpan.FromMinutes(1));

        // Act
        var act = () => score.UpdateIfBetter(-1, 5, TimeSpan.FromMinutes(1));

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Points*");
    }

    [Fact]
    public void UpdateIfBetter_WithNegativeMaxLevel_ShouldThrowException()
    {
        // Arrange
        var score = GameScore.Create("user-id", "game-key", 100, 5, TimeSpan.FromMinutes(1));

        // Act
        var act = () => score.UpdateIfBetter(100, -1, TimeSpan.FromMinutes(1));

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Max level*");
    }

    #endregion
}
