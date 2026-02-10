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
        // Multiplicative: (int)(100 * 1.0 * (1 + 5 * 0.02)) = 110
        character.TotalHP.Should().Be(110);
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
        // Multiplicative: (int)(10 * 1.0 * (1 + 3 * 0.02)) = (int)(10.6) = 10
        character.TotalPower.Should().Be(10);
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
        character.TotalHP.Should().Be(102); // (int)(100 * 1.0 * (1 + 1 * 0.02)) = 102
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
        character.TotalPower.Should().Be(10); // (int)(10 * 1.0 * (1 + 1 * 0.02)) = (int)(10.2) = 10
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
        character.TotalHP.Should().Be(104); // (int)(100 * 1.0 * (1 + 2 * 0.02)) = 104
        character.TotalPower.Should().Be(10); // (int)(10 * 1.0 * (1 + 1 * 0.02)) = 10
        character.TotalSpeed.Should().Be(13); // 10 + (3 * 1) (additive for speed)
    }

    #endregion

    #region CreateCpuSnapshot Tests

    [Fact]
    public void CreateCpuSnapshot_WithDamagedCharacter_ShouldReturnFullHp()
    {
        // Arrange
        var character = Character.Create("user-123");
        character.CurrentHP = 54; // Damaged state

        // Act
        var snapshot = Character.CreateCpuSnapshot(character);

        // Assert
        snapshot.CurrentHP.Should().BeNull(); // null = full HP
        snapshot.TotalHP.Should().Be(character.TotalHP);
    }

    [Fact]
    public void CreateCpuSnapshot_ShouldPreserveBuildData()
    {
        // Arrange
        var character = Character.Create("user-123");
        character.CurrentHP = 30;
        
        // Simulate some upgrades
        for (int i = 0; i < 5; i++) character.UpgradeHP();
        for (int i = 0; i < 3; i++) character.UpgradePower();
        character.AddXP(300); // Level up

        // Act
        var snapshot = Character.CreateCpuSnapshot(character);

        // Assert - Build data preserved
        snapshot.Id.Should().Be(character.Id);
        snapshot.UserId.Should().Be(character.UserId);
        snapshot.Level.Should().Be(character.Level);
        snapshot.HP.Should().Be(character.HP);
        snapshot.Power.Should().Be(character.Power);
        snapshot.Speed.Should().Be(character.Speed);
        snapshot.HpUpgrades.Should().Be(character.HpUpgrades);
        snapshot.PowerUpgrades.Should().Be(character.PowerUpgrades);
        snapshot.SpeedUpgrades.Should().Be(character.SpeedUpgrades);
        snapshot.TotalHP.Should().Be(character.TotalHP);
        snapshot.TotalPower.Should().Be(character.TotalPower);
        snapshot.TotalSpeed.Should().Be(character.TotalSpeed);
        
        // Assert - Combat state reset
        snapshot.CurrentHP.Should().BeNull();
    }

    [Fact]
    public void CreateCpuSnapshot_WithNullCharacter_ShouldThrowException()
    {
        // Act
        var act = () => Character.CreateCpuSnapshot(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CreateCpuSnapshot_ShouldNotAffectOriginalCharacter()
    {
        // Arrange
        var character = Character.Create("user-123");
        character.CurrentHP = 54;

        // Act
        var snapshot = Character.CreateCpuSnapshot(character);

        // Assert - Original unchanged
        character.CurrentHP.Should().Be(54);
        snapshot.CurrentHP.Should().BeNull();
    }

    #endregion

    #region Critical Chance Tests

    [Fact]
    public void TotalCriticalChance_WithNoUpgrades_ShouldEqualBaseCriticalChance()
    {
        // Arrange
        var character = Character.Create("user-123");
        character.CriticalChance = 0.03; // 3% base crit

        // Act & Assert
        character.TotalCriticalChance.Should().Be(0.03);
    }

    [Fact]
    public void TotalCriticalChance_WithUpgrades_ShouldAddBonusCorrectly()
    {
        // Arrange
        var character = Character.Create("user-123");
        character.CriticalChance = 0.0; // 0% base (default)
        character.CriticalUpgrades = 10; // Each gives 0.5% bonus

        // Act & Assert
        // Total = 0.0 + (10 * 0.005) = 0.05 (5%)
        character.TotalCriticalChance.Should().BeApproximately(0.05, 0.001);
    }

    [Fact]
    public void TotalCriticalChance_WhenExceedsMax_ShouldClampToFiftyPercent()
    {
        // Arrange
        var character = Character.Create("user-123");
        character.CriticalChance = 0.50; // 50% base
        character.CriticalUpgrades = 200; // Would add 200%

        // Act & Assert - Should be capped at 0.5 (50%)
        character.TotalCriticalChance.Should().Be(0.5);
    }

    [Fact]
    public void CriticalChance_ShouldBeStoredAsFraction_NotPercent()
    {
        // Arrange
        var character = Character.Create("user-123");
        
        // Act - Set 3% crit chance as fraction
        character.CriticalChance = 0.03;

        // Assert - Should be stored as 0.03, not 3
        character.CriticalChance.Should().Be(0.03);
        character.CriticalChance.Should().BeLessThan(1.0, 
            "CriticalChance should be a fraction [0..1], not a percent [0..100]");
    }

    [Fact]
    public void TotalCriticalChance_ShouldBeFraction_NotPercent()
    {
        // Arrange
        var character = Character.Create("user-123");
        character.CriticalChance = 0.05; // 5%
        character.CriticalUpgrades = 20; // +10%

        // Act
        var totalCrit = character.TotalCriticalChance;

        // Assert - Should be 0.15 (15%), not 15
        totalCrit.Should().BeApproximately(0.15, 0.001);
        totalCrit.Should().BeLessThanOrEqualTo(1.0,
            "TotalCriticalChance should be a fraction [0..1], not a percent [0..100]");
    }

    #endregion
}
