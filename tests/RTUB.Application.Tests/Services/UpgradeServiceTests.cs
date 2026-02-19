using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Application.Repositories;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for UpgradeService
/// Tests upgrade cost calculation and purchase with concurrency safety
/// </summary>
public class UpgradeServiceTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly CharacterService _characterService;
    private readonly UpgradeService _upgradeService;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private ApplicationUser _testUser;

    public UpgradeServiceTests(DatabaseFixture fixture)
    {
        // Clean database at constructor start to ensure test isolation
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();

        _context = _fixture.CreateContext();
        var characterRepository = new CharacterRepository(_context);
        var mockCharUserStore = new Mock<IUserStore<ApplicationUser>>();
        var mockCharUserManager = new Mock<UserManager<ApplicationUser>>(
            mockCharUserStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        var mockCharConfig = Options.Create(new MyTunoScalingConfiguration());
        var mockCharLogger = new Mock<ILogger<CharacterService>>();
        _characterService = new CharacterService(
            characterRepository, mockCharUserManager.Object, mockCharConfig, mockCharLogger.Object, _context);

        // Setup UserManager mock
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        _mockUserManager = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        // Setup MyTunoScalingConfiguration with base costs
        var config = new MyTunoScalingConfiguration
        {
            Upgrades = new MyTunoUpgrades
            {
                HP = new UpgradeLogStat { BaseCost = 50m, CostPerLevel = 50m },
                Power = new UpgradeLogStat { BaseCost = 75m, CostPerLevel = 75m },
                Speed = new UpgradeFlatStat { BaseCost = 100m, CostPerLevel = 100m },
                CriticalChance = new UpgradeFlatStat { BaseCost = 150m, CostPerLevel = 150m }
            }
        };
        var mockConfig = new Mock<IOptions<MyTunoScalingConfiguration>>();
        mockConfig.Setup(m => m.Value).Returns(config);

        _upgradeService = new UpgradeService(
            _characterService,
            _mockUserManager.Object,
            _context,
            mockConfig.Object,
            new Mock<IInventoryRepository>().Object);

        // Create test user
        var userId = "upgrade-test-user";
        if (!_context.Users.Any(u => u.Id == userId))
        {
            _testUser = new ApplicationUser
            {
                Id = userId,
                UserName = "upgrade_testuser",
                Email = "upgrade_test@test.com",
                FirstName = "Upgrade",
                LastName = "Test",
                Nickname = "UpgradeTestUser",
                FidelisBalance = 1000m
            };
            _context.Users.Add(_testUser);
            _context.SaveChanges();
        }
        else
        {
            _testUser = _context.Users.Find(userId)!;
            _testUser.FidelisBalance = 1000m; // Reset balance for tests
            _context.SaveChanges();
        }

        // Setup UserManager mock to return test user
        _mockUserManager
            .Setup(m => m.FindByIdAsync(_testUser.Id))
            .ReturnsAsync(_testUser);

        _mockUserManager
            .Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region GetUpgradeCostAsync Tests

    [Fact]
    public async Task GetUpgradeCostAsync_ForHP_WithNoUpgrades_ShouldReturnBaseCost()
    {
        // Arrange
        var character = await _characterService.GetOrCreateCharacterAsync(_testUser.Id);
        character.HpUpgrades = 0;

        // Act
        var cost = await _upgradeService.GetUpgradeCostAsync(_testUser.Id, StatType.HP);

        // Assert
        cost.Should().Be(50m); // BaseCostHP
    }

    [Fact]
    public async Task GetUpgradeCostAsync_ForPower_WithNoUpgrades_ShouldReturnBaseCost()
    {
        // Arrange
        var character = await _characterService.GetOrCreateCharacterAsync(_testUser.Id);
        character.PowerUpgrades = 0;

        // Act
        var cost = await _upgradeService.GetUpgradeCostAsync(_testUser.Id, StatType.Power);

        // Assert
        cost.Should().Be(75m); // BaseCostPower
    }

    [Fact]
    public async Task GetUpgradeCostAsync_ForSpeed_WithNoUpgrades_ShouldReturnBaseCost()
    {
        // Arrange
        var character = await _characterService.GetOrCreateCharacterAsync(_testUser.Id);
        character.SpeedUpgrades = 0;

        // Act
        var cost = await _upgradeService.GetUpgradeCostAsync(_testUser.Id, StatType.Speed);

        // Assert
        cost.Should().Be(100m); // BaseCostSpeed
    }

    [Fact]
    public async Task GetUpgradeCostAsync_WithUpgrades_ShouldScaleCorrectly()
    {
        // Arrange
        var character = await _characterService.GetOrCreateCharacterAsync(_testUser.Id);
        character.HpUpgrades = 2; // 2 upgrades already purchased
        await _context.SaveChangesAsync(); // Persist so cost lookup reads the correct value

        // Act
        var cost = await _upgradeService.GetUpgradeCostAsync(_testUser.Id, StatType.HP);

        // Assert
        // Linear formula: BaseCost(50) + UpgradeCount(2) * CostPerLevel(50) = 50 + 100 = 150
        cost.Should().Be(150m);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task GetUpgradeCostAsync_WithEmptyUserId_ShouldThrowException(string? userId)
    {
        // Act
        var act = async () => await _upgradeService.GetUpgradeCostAsync(userId!, StatType.HP);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*User ID*");
    }

    #endregion

    #region PurchaseUpgradeAsync Tests

    [Fact]
    public async Task PurchaseUpgradeAsync_WithSufficientFidelis_ShouldSucceed()
    {
        // Arrange
        _testUser.FidelisBalance = 1000m;
        _context.SaveChanges();
        var character = await _characterService.GetOrCreateCharacterAsync(_testUser.Id);
        var initialHpUpgrades = character.HpUpgrades;
        var initialBalance = _testUser.FidelisBalance;

        // Act
        var result = await _upgradeService.PurchaseUpgradeAsync(_testUser.Id, StatType.HP);

        // Assert
        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.NewUpgradeCount.Should().Be(initialHpUpgrades + 1);
        result.NewFidelisBalance.Should().BeLessThan(initialBalance);

        // Verify character was updated
        var updatedCharacter = await _characterService.GetCharacterAsync(_testUser.Id);
        updatedCharacter.Should().NotBeNull();
        updatedCharacter!.HpUpgrades.Should().Be(initialHpUpgrades + 1);
    }

    [Fact]
    public async Task PurchaseUpgradeAsync_WithInsufficientFidelis_ShouldFail()
    {
        // Arrange
        _testUser.FidelisBalance = 1m; // Very low balance
        _context.SaveChanges();

        // Act
        var result = await _upgradeService.PurchaseUpgradeAsync(_testUser.Id, StatType.HP);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Saldo de Fidelis insuficiente");
        result.NewFidelisBalance.Should().Be(0m);
        result.NewUpgradeCount.Should().Be(0);
    }

    [Fact]
    public async Task PurchaseUpgradeAsync_ForPower_ShouldIncrementPowerUpgrades()
    {
        // Arrange
        _testUser.FidelisBalance = 1000m;
        _context.SaveChanges();
        var character = await _characterService.GetOrCreateCharacterAsync(_testUser.Id);
        var initialPowerUpgrades = character.PowerUpgrades;

        // Act
        var result = await _upgradeService.PurchaseUpgradeAsync(_testUser.Id, StatType.Power);

        // Assert
        result.Success.Should().BeTrue();
        result.NewUpgradeCount.Should().Be(initialPowerUpgrades + 1);

        var updatedCharacter = await _characterService.GetCharacterAsync(_testUser.Id);
        updatedCharacter!.PowerUpgrades.Should().Be(initialPowerUpgrades + 1);
    }

    [Fact]
    public async Task PurchaseUpgradeAsync_ForSpeed_ShouldIncrementSpeedUpgrades()
    {
        // Arrange
        _testUser.FidelisBalance = 1000m;
        _context.SaveChanges();
        var character = await _characterService.GetOrCreateCharacterAsync(_testUser.Id);
        var initialSpeedUpgrades = character.SpeedUpgrades;

        // Act
        var result = await _upgradeService.PurchaseUpgradeAsync(_testUser.Id, StatType.Speed);

        // Assert
        result.Success.Should().BeTrue();
        result.NewUpgradeCount.Should().Be(initialSpeedUpgrades + 1);

        var updatedCharacter = await _characterService.GetCharacterAsync(_testUser.Id);
        updatedCharacter!.SpeedUpgrades.Should().Be(initialSpeedUpgrades + 1);
    }

    [Fact]
    public async Task PurchaseUpgradeAsync_MultipleUpgrades_ShouldScaleCost()
    {
        // Arrange
        _testUser.FidelisBalance = 10000m;
        _context.SaveChanges();
        var character = await _characterService.GetOrCreateCharacterAsync(_testUser.Id);
        var initialBalance = _testUser.FidelisBalance;

        // Act - Purchase 3 HP upgrades
        var result1 = await _upgradeService.PurchaseUpgradeAsync(_testUser.Id, StatType.HP);
        var result2 = await _upgradeService.PurchaseUpgradeAsync(_testUser.Id, StatType.HP);
        var result3 = await _upgradeService.PurchaseUpgradeAsync(_testUser.Id, StatType.HP);

        // Assert
        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
        result3.Success.Should().BeTrue();

        // Each upgrade should cost more than the previous
        var cost1 = initialBalance - result1.NewFidelisBalance;
        var cost2 = result1.NewFidelisBalance - result2.NewFidelisBalance;
        var cost3 = result2.NewFidelisBalance - result3.NewFidelisBalance;

        cost2.Should().BeGreaterThan(cost1);
        cost3.Should().BeGreaterThan(cost2);

        var updatedCharacter = await _characterService.GetCharacterAsync(_testUser.Id);
        updatedCharacter!.HpUpgrades.Should().Be(3);
    }

    [Fact]
    public async Task PurchaseUpgradeAsync_FidelisNeverGoesNegative()
    {
        // Arrange
        _testUser.FidelisBalance = 50m; // Exactly base cost for HP
        _context.SaveChanges();

        // Act
        var result = await _upgradeService.PurchaseUpgradeAsync(_testUser.Id, StatType.HP);

        // Assert
        if (result.Success)
        {
            result.NewFidelisBalance.Should().BeGreaterThanOrEqualTo(0m);
        }
        else
        {
            // If it fails, balance should remain unchanged or error should indicate insufficient funds
            result.ErrorMessage.Should().Contain("insuficiente");
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task PurchaseUpgradeAsync_WithEmptyUserId_ShouldThrowException(string? userId)
    {
        // Act
        var act = async () => await _upgradeService.PurchaseUpgradeAsync(userId!, StatType.HP);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*User ID*");
    }

    #endregion
}
