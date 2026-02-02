using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Core.Tests.Entities;

public class CharacterTests
{
    #region Create Tests

    [Fact]
    public void Create_WithValidUserId_ShouldCreateInstance()
    {
        // Arrange
        var userId = "user-123";

        // Act
        var character = Character.Create(userId);

        // Assert
        character.Should().NotBeNull();
        character.UserId.Should().Be(userId);
        character.Level.Should().Be(1);
        character.XP.Should().Be(0);
        character.HP.Should().Be(100);
        character.Power.Should().Be(10);
        character.Speed.Should().Be(10);
        character.HpUpgrades.Should().Be(0);
        character.PowerUpgrades.Should().Be(0);
        character.SpeedUpgrades.Should().Be(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_WithEmptyUserId_ShouldThrowException(string? userId)
    {
        // Act
        var act = () => Character.Create(userId!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*User ID*");
    }

    #endregion

    #region Stat Calculations Tests

    [Fact]
    public void TotalHP_WithNoUpgrades_ShouldReturnBaseHP()
    {
        // Arrange
        var character = Character.Create("user-123");

        // Act & Assert
        character.TotalHP.Should().Be(100);
    }

    [Fact]
    public void TotalHP_WithUpgrades_ShouldReturnBasePlusUpgrades()
    {
        // Arrange
        var character = Character.Create("user-123");
        character.HpUpgrades = 5;

        // Act & Assert
        character.TotalHP.Should().Be(100 + (5 * 10)); // 100 + 50 = 150
    }

    [Fact]
    public void TotalPower_WithNoUpgrades_ShouldReturnBasePower()
    {
        // Arrange
        var character = Character.Create("user-123");

        // Act & Assert
        character.TotalPower.Should().Be(10);
    }

    [Fact]
    public void TotalPower_WithUpgrades_ShouldReturnBasePlusUpgrades()
    {
        // Arrange
        var character = Character.Create("user-123");
        character.PowerUpgrades = 3;

        // Act & Assert
        character.TotalPower.Should().Be(10 + (3 * 2)); // 10 + 6 = 16
    }

    [Fact]
    public void TotalSpeed_WithNoUpgrades_ShouldReturnBaseSpeed()
    {
        // Arrange
        var character = Character.Create("user-123");

        // Act & Assert
        character.TotalSpeed.Should().Be(10);
    }

    [Fact]
    public void TotalSpeed_WithUpgrades_ShouldReturnBasePlusUpgrades()
    {
        // Arrange
        var character = Character.Create("user-123");
        character.SpeedUpgrades = 4;

        // Act & Assert
        character.TotalSpeed.Should().Be(10 + (4 * 1)); // 10 + 4 = 14
    }

    #endregion

    #region AddXP Tests

    [Fact]
    public void AddXP_WithValidAmount_ShouldAddXP()
    {
        // Arrange
        var character = Character.Create("user-123");

        // Act
        character.AddXP(50);

        // Assert
        character.XP.Should().Be(50);
        character.Level.Should().Be(1);
    }

    [Fact]
    public void AddXP_WithEnoughForLevelUp_ShouldLevelUp()
    {
        // Arrange
        var character = Character.Create("user-123");

        // Act
        character.AddXP(100); // Level 1 needs 100 XP

        // Assert
        character.Level.Should().Be(2);
        character.XP.Should().Be(0); // XP reset after level up
    }

    [Fact]
    public void AddXP_WithMultipleLevelUps_ShouldLevelUpMultipleTimes()
    {
        // Arrange
        var character = Character.Create("user-123");

        // Act
        character.AddXP(250); // 100 for L1->L2 (100 XP used, Level 2, 150 remaining), then 150 < 200, so Level 2 with 150 XP

        // Assert
        character.Level.Should().Be(2);
        character.XP.Should().Be(150);
    }

    [Fact]
    public void AddXP_WithExactLevelUpAmount_ShouldLevelUpWithZeroXP()
    {
        // Arrange
        var character = Character.Create("user-123");
        character.Level = 2; // Level 2 needs 200 XP

        // Act
        character.AddXP(200);

        // Assert
        character.Level.Should().Be(3);
        character.XP.Should().Be(0);
    }

    [Fact]
    public void AddXP_WithNegativeAmount_ShouldThrowException()
    {
        // Arrange
        var character = Character.Create("user-123");

        // Act
        var act = () => character.AddXP(-10);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*XP amount*");
    }

    [Fact]
    public void AddXP_WithZero_ShouldNotChangeXP()
    {
        // Arrange
        var character = Character.Create("user-123");

        // Act
        character.AddXP(0);

        // Assert
        character.XP.Should().Be(0);
        character.Level.Should().Be(1);
    }

    #endregion

    #region Upgrade Methods Tests

    [Fact]
    public void UpgradeHP_ShouldIncrementHpUpgrades()
    {
        // Arrange
        var character = Character.Create("user-123");

        // Act
        character.UpgradeHP();

        // Assert
        character.HpUpgrades.Should().Be(1);
        character.TotalHP.Should().Be(110); // 100 + (1 * 10)
    }

    [Fact]
    public void UpgradePower_ShouldIncrementPowerUpgrades()
    {
        // Arrange
        var character = Character.Create("user-123");

        // Act
        character.UpgradePower();

        // Assert
        character.PowerUpgrades.Should().Be(1);
        character.TotalPower.Should().Be(12); // 10 + (1 * 2)
    }

    [Fact]
    public void UpgradeSpeed_ShouldIncrementSpeedUpgrades()
    {
        // Arrange
        var character = Character.Create("user-123");

        // Act
        character.UpgradeSpeed();

        // Assert
        character.SpeedUpgrades.Should().Be(1);
        character.TotalSpeed.Should().Be(11); // 10 + (1 * 1)
    }

    [Fact]
    public void MultipleUpgrades_ShouldAccumulate()
    {
        // Arrange
        var character = Character.Create("user-123");

        // Act
        character.UpgradeHP();
        character.UpgradeHP();
        character.UpgradePower();
        character.UpgradeSpeed();
        character.UpgradeSpeed();
        character.UpgradeSpeed();

        // Assert
        character.HpUpgrades.Should().Be(2);
        character.PowerUpgrades.Should().Be(1);
        character.SpeedUpgrades.Should().Be(3);
        character.TotalHP.Should().Be(120); // 100 + (2 * 10)
        character.TotalPower.Should().Be(12); // 10 + (1 * 2)
        character.TotalSpeed.Should().Be(13); // 10 + (3 * 1)
    }

    #endregion
}
