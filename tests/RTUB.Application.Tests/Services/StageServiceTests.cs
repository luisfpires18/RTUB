using FluentAssertions;
using Microsoft.AspNetCore.Http;
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
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for StageService
/// Tests stage progression, battle execution, and reward distribution
/// </summary>
public class StageServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly IStageProgressRepository _stageProgressRepository;
    private readonly IStageEnemyRepository _stageEnemyRepository;
    private readonly ICharacterRepository _characterRepository;
    private readonly Mock<ICombatEngine> _combatEngineMock;
    private readonly Mock<IInventoryRepository> _inventoryRepositoryMock;
    private readonly Mock<ILogger<StageService>> _loggerMock;
    private readonly Mock<IOptions<MyTunoScalingConfiguration>> _myTunoScalingConfigMock;
    private readonly Mock<IStageBiomeService> _biomeServiceMock;
    private readonly IStageService _stageService;

    public StageServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(
            options,
            Mock.Of<IHttpContextAccessor>(),
            new AuditContext(),
            new AuditLogAppender());

        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _stageProgressRepository = new StageProgressRepository(_context);
        _stageEnemyRepository = new StageEnemyRepository(_context);
        _characterRepository = new CharacterRepository(_context);
        _combatEngineMock = new Mock<ICombatEngine>();
        _inventoryRepositoryMock = new Mock<IInventoryRepository>();
        _loggerMock = new Mock<ILogger<StageService>>();

        // Setup MyTunoScalingConfiguration mock with default values
        var myTunoScalingConfig = new MyTunoScalingConfiguration
        {
            BattleRewards = new BattleRewards
            {
                WinReward = 10m,
                DrawReward = 7.5m
            },
            StageMode = new StageModeConfig
            {
                EnemyTiers = new List<StageEnemyTierConfig>
                {
                    new StageEnemyTierConfig { Tier = 1, MinStage = 1, MaxStage = 999999999, HP = 150, Power = 15, Defense = 5, Speed = 10, CritChance = 0.03, FidelisReward = 10m, XpReward = 10, BossHP = 500, BossPower = 50, BossDefense = 25 }
                }
            }
        };
        _myTunoScalingConfigMock = new Mock<IOptions<MyTunoScalingConfiguration>>();
        _myTunoScalingConfigMock.Setup(x => x.Value).Returns(myTunoScalingConfig);

        // Setup BiomeService mock
        _biomeServiceMock = new Mock<IStageBiomeService>();
        _biomeServiceMock.Setup(x => x.IsBossStage(It.IsAny<int>())).Returns((int stage) => stage % 10 == 0);
        _biomeServiceMock.Setup(x => x.GetBiomeForStage(It.IsAny<int>())).Returns("Forest");
        _biomeServiceMock.Setup(x => x.GetEnemyCountForStage(It.IsAny<int>())).Returns(1);
        _biomeServiceMock.Setup(x => x.GetRandomEnemySpritesAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((int stage, int count) => Enumerable.Repeat("/images/enemies/default.png", count).ToList());
        _biomeServiceMock.Setup(x => x.GetRandomEnemySpritesWithPlacementAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((int stage, int count) => Enumerable.Repeat(("/images/enemies/default.png", 0), count).ToList());
        _biomeServiceMock.Setup(x => x.GetBossSpriteAsync(It.IsAny<int>()))
            .ReturnsAsync("/images/enemies/boss.png");
        _biomeServiceMock.Setup(x => x.GetEnemiesDifficultyMultiplier(It.IsAny<int>())).Returns(1.0);
        _biomeServiceMock.Setup(x => x.GetBossesDifficultyMultiplier(It.IsAny<int>())).Returns(1.0);
        _biomeServiceMock.Setup(x => x.GetRewardMultiplierForStage(It.IsAny<int>())).Returns(1.0);
        _biomeServiceMock.Setup(x => x.GetEnemyTierForStage(It.IsAny<int>())).Returns(new StageEnemyTierConfig { Tier = 1, MinStage = 1, MaxStage = 999999999, HP = 150, Power = 15, Defense = 5, Speed = 10, CritChance = 0.03, FidelisReward = 10m, XpReward = 10, BossHP = 500, BossPower = 50, BossDefense = 25 });

        _stageService = new StageService(
            _stageProgressRepository,
            _stageEnemyRepository,
            _characterRepository,
            _combatEngineMock.Object,
            _inventoryRepositoryMock.Object,
            _userManagerMock.Object,
            _loggerMock.Object,
            _myTunoScalingConfigMock.Object,
            _biomeServiceMock.Object);
    }

    /// <summary>
    /// Helper method to create a test user with all required fields
    /// </summary>
    private static ApplicationUser CreateTestUser(string userId = "user1", string userName = "testuser", decimal fidelisBalance = 100m)
    {
        return new ApplicationUser
        {
            Id = userId,
            UserName = userName,
            Email = $"{userName}@test.com",
            FirstName = "Test",
            LastName = "User",
            Nickname = userName,
            FidelisBalance = fidelisBalance
        };
    }

    #region GetOrCreateStageProgressAsync Tests

    [Fact]
    public async Task GetOrCreateStageProgressAsync_WhenNoProgressExists_ShouldCreateNew()
    {
        // Arrange
        var userId = "user1";
        var user = CreateTestUser(userId);
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // Act
        var progress = await _stageService.GetOrCreateStageProgressAsync(userId);

        // Assert
        progress.Should().NotBeNull();
        progress.UserId.Should().Be(userId);
        progress.CurrentStage.Should().Be(1);
        progress.HighestStage.Should().Be(1);
        progress.LastCheckpoint.Should().Be(1);
        progress.CurrentRegion.Should().Be(RegionType.Forest);
        progress.EndlessModeUnlocked.Should().BeFalse();
        progress.TotalStagesCleared.Should().Be(0);
        progress.TotalBossesDefeated.Should().Be(0);

        // Verify it was persisted
        var savedProgress = await _stageProgressRepository.GetByUserIdAsync(userId);
        savedProgress.Should().NotBeNull();
        savedProgress!.Id.Should().Be(progress.Id);
    }

    [Fact]
    public async Task GetOrCreateStageProgressAsync_WhenProgressExists_ShouldReturnExisting()
    {
        // Arrange
        var userId = "user1";
        var user = CreateTestUser(userId);
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var existingProgress = StageProgress.Create(userId);
        existingProgress.AdvanceStage(); // Move to stage 2
        await _stageProgressRepository.AddAsync(existingProgress);

        // Act
        var progress = await _stageService.GetOrCreateStageProgressAsync(userId);

        // Assert
        progress.Should().NotBeNull();
        progress.Id.Should().Be(existingProgress.Id);
        progress.CurrentStage.Should().Be(2);
        progress.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task GetOrCreateStageProgressAsync_WithNullUserId_ShouldThrow()
    {
        // Act
        var act = () => _stageService.GetOrCreateStageProgressAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*User ID is required*");
    }

    [Fact]
    public async Task GetOrCreateStageProgressAsync_WithEmptyUserId_ShouldThrow()
    {
        // Act
        var act = () => _stageService.GetOrCreateStageProgressAsync("");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*User ID is required*");
    }

    #endregion

    #region GetStageProgressAsync Tests

    [Fact]
    public async Task GetStageProgressAsync_WhenProgressExists_ShouldReturn()
    {
        // Arrange
        var userId = "user1";
        var user = CreateTestUser(userId);
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var existingProgress = StageProgress.Create(userId);
        await _stageProgressRepository.AddAsync(existingProgress);

        // Act
        var progress = await _stageService.GetStageProgressAsync(userId);

        // Assert
        progress.Should().NotBeNull();
        progress!.UserId.Should().Be(userId);
        progress.Id.Should().Be(existingProgress.Id);
    }

    [Fact]
    public async Task GetStageProgressAsync_WhenNoProgress_ShouldReturnNull()
    {
        // Arrange
        var userId = "user1";

        // Act
        var progress = await _stageService.GetStageProgressAsync(userId);

        // Assert
        progress.Should().BeNull();
    }

    [Fact]
    public async Task GetStageProgressAsync_WithNullUserId_ShouldThrow()
    {
        // Act
        var act = () => _stageService.GetStageProgressAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*User ID is required*");
    }

    #endregion

    #region ExecuteStageBattleAsync Tests

    [Fact]
    public async Task ExecuteStageBattleAsync_WithValidInputs_ShouldCreateBattle()
    {
        // Arrange
        var user = CreateTestUser();
        await _context.Users.AddAsync(user);

        var character = Character.Create("user1");
        await _context.Characters.AddAsync(character);
        await _context.SaveChangesAsync();

        _userManagerMock.Setup(m => m.FindByIdAsync("user1"))
            .ReturnsAsync(user);
        _userManagerMock.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        var combatResult = new Application.DTOs.CombatResult
        {
            Outcome = BattleOutcome.AttackerWon,
            Events = new List<Application.DTOs.CombatEvent>
            {
                new() { Type = "Victory", Winner = "Attacker", Timestamp = 0 }
            },
            AttackerFinalHP = 90,
            DefenderFinalHP = 0
        };

        _combatEngineMock.Setup(e => e.Simulate(It.IsAny<Character>(), It.IsAny<Character>(), It.IsAny<int>()))
            .Returns(combatResult);

        // Act
        var battle = await _stageService.ExecuteStageBattleAsync(character.Id);

        // Assert - StageBattleResult is returned but not persisted
        battle.Should().NotBeNull();
        battle.BattleId.Should().NotBe(Guid.Empty);
        battle.CharacterId.Should().Be(character.Id);
        battle.StageNumber.Should().Be(1);
        battle.EnemyType.Should().Be(EnemyType.Normal);
        battle.Region.Should().Be(RegionType.Forest);
        battle.Outcome.Should().Be(BattleOutcome.AttackerWon);
        battle.XPReward.Should().BeGreaterThan(0);
        battle.FidelisReward.Should().BeGreaterThan(0);
        battle.ReplayJson.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExecuteStageBattleAsync_WithInvalidCharacter_ShouldThrow()
    {
        // Act
        var act = () => _stageService.ExecuteStageBattleAsync(999);

        // Assert
        await act.Should().ThrowAsync<Core.Exceptions.EntityNotFoundException>();
    }

    [Fact]
    public async Task ExecuteStageBattleAsync_WithDeadCharacter_ShouldStillWork()
    {
        // Arrange - player no longer dies in stage mode, so even a character at 0 HP can fight
        var user = CreateTestUser();
        await _context.Users.AddAsync(user);

        var character = Character.Create("user1");
        character.CurrentHP = 0;
        await _context.Characters.AddAsync(character);
        await _context.SaveChangesAsync();

        _userManagerMock.Setup(m => m.FindByIdAsync("user1"))
            .ReturnsAsync(user);
        _userManagerMock.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        var combatResult = new Application.DTOs.CombatResult
        {
            Outcome = BattleOutcome.AttackerWon,
            Events = new List<Application.DTOs.CombatEvent>
            {
                new() { Type = "Victory", Winner = "Attacker", Timestamp = 0 }
            },
            AttackerFinalHP = 50,
            DefenderFinalHP = 0
        };

        _combatEngineMock.Setup(e => e.Simulate(It.IsAny<Character>(), It.IsAny<Character>(), It.IsAny<int>()))
            .Returns(combatResult);

        // Act - should not throw
        var battle = await _stageService.ExecuteStageBattleAsync(character.Id);

        // Assert
        battle.Should().NotBeNull();
        battle.Outcome.Should().Be(BattleOutcome.AttackerWon);
    }

    [Fact]
    public async Task ExecuteStageBattleAsync_WhenPlayerWins_ShouldAdvanceStage()
    {
        // Arrange
        var user = CreateTestUser();
        await _context.Users.AddAsync(user);

        var character = Character.Create("user1");
        await _context.Characters.AddAsync(character);
        await _context.SaveChangesAsync();

        _userManagerMock.Setup(m => m.FindByIdAsync("user1"))
            .ReturnsAsync(user);
        _userManagerMock.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        var combatResult = new Application.DTOs.CombatResult
        {
            Outcome = BattleOutcome.AttackerWon,
            Events = new List<Application.DTOs.CombatEvent>
            {
                new() { Type = "Victory", Winner = "Attacker", Timestamp = 0 }
            },
            AttackerFinalHP = 50,
            DefenderFinalHP = 0
        };

        _combatEngineMock.Setup(e => e.Simulate(It.IsAny<Character>(), It.IsAny<Character>(), It.IsAny<int>()))
            .Returns(combatResult);

        // Act
        var battle = await _stageService.ExecuteStageBattleAsync(character.Id);

        // Assert
        var progress = await _stageProgressRepository.GetByUserIdAsync("user1");
        progress.Should().NotBeNull();
        progress!.CurrentStage.Should().Be(2); // Advanced from 1 to 2
        progress.HighestStage.Should().Be(2);
        progress.TotalStagesCleared.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteStageBattleAsync_WhenPlayerWins_ShouldApplyRewards()
    {
        // Arrange
        var user = CreateTestUser();
        await _context.Users.AddAsync(user);

        var character = Character.Create("user1");
        var initialXP = character.XP;
        var initialFidelis = user.FidelisBalance;
        await _context.Characters.AddAsync(character);
        await _context.SaveChangesAsync();

        _userManagerMock.Setup(m => m.FindByIdAsync("user1"))
            .ReturnsAsync(user);
        _userManagerMock.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        var combatResult = new Application.DTOs.CombatResult
        {
            Outcome = BattleOutcome.AttackerWon,
            Events = new List<Application.DTOs.CombatEvent>
            {
                new() { Type = "Victory", Winner = "Attacker", Timestamp = 0 }
            },
            AttackerFinalHP = 50,
            DefenderFinalHP = 0
        };

        _combatEngineMock.Setup(e => e.Simulate(It.IsAny<Character>(), It.IsAny<Character>(), It.IsAny<int>()))
            .Returns(combatResult);

        // Act
        var battle = await _stageService.ExecuteStageBattleAsync(character.Id);

        // Assert - rewards are calculated but NOT applied immediately (deferred until run ends)
        // v5: tier-based flat XP: tier.XpReward(10) * enemyCount(1) = 10
        battle.XPReward.Should().Be(10);
        battle.FidelisReward.Should().Be(10m); // round(tier.FidelisReward(10) * enemyCount(1) * biomeRewardMult(1.0), 2) = 10

        // Verify character XP was NOT updated yet (deferred rewards)
        var updatedCharacter = await _characterRepository.GetByIdAsync(character.Id);
        updatedCharacter!.XP.Should().Be(initialXP);

        // Verify user Fidelis was NOT updated yet (deferred rewards)
        _userManagerMock.Verify(m => m.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);

        // Act - now apply deferred rewards (restore HP to pre-run value of 3000)
        await _stageService.ApplyRunRewardsAsync(character.Id, battle.XPReward, battle.FidelisReward, 0, 0, 0, 0, 0, restoreHp: 3000);

        // Assert - rewards are now applied and HP is restored to pre-run value
        var finalCharacter = await _characterRepository.GetByIdAsync(character.Id);
        finalCharacter!.XP.Should().Be(initialXP + 10);
        finalCharacter.CurrentHP.Should().Be(3000, "HP should be restored to the value before the stage run started");

        _userManagerMock.Verify(m => m.UpdateAsync(It.Is<ApplicationUser>(u =>
            u.FidelisBalance == initialFidelis + 10m)), Times.Once);
    }

    [Fact]
    public async Task ExecuteStageBattleAsync_WhenPlayerLoses_ShouldNotAdvanceStage()
    {
        // Arrange
        var user = CreateTestUser();
        await _context.Users.AddAsync(user);

        var character = Character.Create("user1");
        await _context.Characters.AddAsync(character);
        await _context.SaveChangesAsync();

        _userManagerMock.Setup(m => m.FindByIdAsync("user1"))
            .ReturnsAsync(user);
        _userManagerMock.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        var combatResult = new Application.DTOs.CombatResult
        {
            Outcome = BattleOutcome.DefenderWon,
            Events = new List<Application.DTOs.CombatEvent>
            {
                new() { Type = "Victory", Winner = "Defender", Timestamp = 0 }
            },
            AttackerFinalHP = 0,
            DefenderFinalHP = 50
        };

        _combatEngineMock.Setup(e => e.Simulate(It.IsAny<Character>(), It.IsAny<Character>(), It.IsAny<int>()))
            .Returns(combatResult);

        // Act
        var battle = await _stageService.ExecuteStageBattleAsync(character.Id);

        // Assert
        var progress = await _stageProgressRepository.GetByUserIdAsync("user1");
        progress.Should().NotBeNull();
        progress!.CurrentStage.Should().Be(1); // Should remain at stage 1
        progress.TotalStagesCleared.Should().Be(0); // No stages cleared

        // Character HP should be restored to full (no death in stage mode)
        var updatedCharacter = await _characterRepository.GetByIdAsync(character.Id);
        updatedCharacter!.CurrentHP.Should().BeNull(); // null = full HP
    }

    [Fact]
    public async Task ExecuteStageBattleAsync_WhenPlayerLoses_ShouldApplyConsolationRewards()
    {
        // Arrange
        var user = CreateTestUser();
        await _context.Users.AddAsync(user);

        var character = Character.Create("user1");
        var initialXP = character.XP;
        await _context.Characters.AddAsync(character);
        await _context.SaveChangesAsync();

        _userManagerMock.Setup(m => m.FindByIdAsync("user1"))
            .ReturnsAsync(user);
        _userManagerMock.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        var combatResult = new Application.DTOs.CombatResult
        {
            Outcome = BattleOutcome.DefenderWon,
            Events = new List<Application.DTOs.CombatEvent>
            {
                new() { Type = "Victory", Winner = "Defender", Timestamp = 0 }
            },
            AttackerFinalHP = 0,
            DefenderFinalHP = 50
        };

        _combatEngineMock.Setup(e => e.Simulate(It.IsAny<Character>(), It.IsAny<Character>(), It.IsAny<int>()))
            .Returns(combatResult);

        // Act
        var battle = await _stageService.ExecuteStageBattleAsync(character.Id);

        // Assert
        // Stage mode gives no rewards on defeat
        battle.XPReward.Should().Be(0);
        battle.FidelisReward.Should().Be(0m);
        battle.FinosDropped.Should().Be(0);
        battle.ShotsDropped.Should().Be(0);

        // Verify character XP was NOT updated (no consolation rewards)
        var updatedCharacter = await _characterRepository.GetByIdAsync(character.Id);
        updatedCharacter!.XP.Should().Be(initialXP);

        // Character HP should be restored to full (no death in stage mode)
        updatedCharacter.CurrentHP.Should().BeNull(); // null = full HP

        // Verify user Fidelis was NOT updated (no rewards on defeat)
        _userManagerMock.Verify(m => m.UpdateAsync(It.Is<ApplicationUser>(u =>
            u.FidelisBalance == 100m)), Times.Never);
    }

    [Fact]
    public async Task ExecuteStageBattleAsync_WhenFightingBoss_ShouldGiveTenTimesXP()
    {
        // Arrange
        var user = CreateTestUser();
        await _context.Users.AddAsync(user);

        var character = Character.Create("user1");
        await _context.Characters.AddAsync(character);
        await _context.SaveChangesAsync();

        // Create progress at stage 100 (boss stage: stage%100==0)
        var progress = StageProgress.Create("user1");
        progress.CurrentStage = 100;
        progress.HighestStage = 100;
        progress.LastCheckpoint = 91;
        progress.CurrentRegion = RegionType.Forest;
        await _stageProgressRepository.AddAsync(progress);

        _userManagerMock.Setup(m => m.FindByIdAsync("user1"))
            .ReturnsAsync(user);
        _userManagerMock.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        var combatResult = new Application.DTOs.CombatResult
        {
            Outcome = BattleOutcome.AttackerWon,
            Events = new List<Application.DTOs.CombatEvent>
            {
                new() { Type = "Victory", Winner = "Attacker", Timestamp = 0 }
            },
            AttackerFinalHP = 50,
            DefenderFinalHP = 0
        };

        _combatEngineMock.Setup(e => e.Simulate(It.IsAny<Character>(), It.IsAny<Character>(), It.IsAny<int>()))
            .Returns(combatResult);

        // Act
        var battle = await _stageService.ExecuteStageBattleAsync(character.Id);

        // Assert
        battle.EnemyType.Should().Be(EnemyType.Boss);
        // v5: tier-based flat XP = tier.XpReward(10) * enemyCount(1) = 10
        battle.XPReward.Should().Be(10);

        // Verify boss defeat was recorded
        var updatedProgress = await _stageProgressRepository.GetByUserIdAsync("user1");
        updatedProgress!.TotalBossesDefeated.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteStageBattleAsync_WhenFightingBossAtStage20000_ShouldUnlockEndlessMode()
    {
        // Arrange
        var user = CreateTestUser();
        await _context.Users.AddAsync(user);

        var character = Character.Create("user1");
        await _context.Characters.AddAsync(character);
        await _context.SaveChangesAsync();

        // Create progress at stage 20000 (final Light boss)
        var progress = StageProgress.Create("user1");
        progress.CurrentStage = 20000;
        progress.HighestStage = 20000;
        progress.LastCheckpoint = 19991;
        progress.CurrentRegion = RegionType.Light;
        await _stageProgressRepository.AddAsync(progress);

        _userManagerMock.Setup(m => m.FindByIdAsync("user1"))
            .ReturnsAsync(user);
        _userManagerMock.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        var combatResult = new Application.DTOs.CombatResult
        {
            Outcome = BattleOutcome.AttackerWon,
            Events = new List<Application.DTOs.CombatEvent>
            {
                new() { Type = "Victory", Winner = "Attacker", Timestamp = 0 }
            },
            AttackerFinalHP = 50,
            DefenderFinalHP = 0
        };

        _combatEngineMock.Setup(e => e.Simulate(It.IsAny<Character>(), It.IsAny<Character>(), It.IsAny<int>()))
            .Returns(combatResult);

        // Act
        var battle = await _stageService.ExecuteStageBattleAsync(character.Id);

        // Assert
        battle.EnemyType.Should().Be(EnemyType.Boss);
        battle.StageNumber.Should().Be(20000);

        // Verify endless mode was unlocked
        var updatedProgress = await _stageProgressRepository.GetByUserIdAsync("user1");
        updatedProgress!.EndlessModeUnlocked.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteStageBattleAsync_ShouldPreserveHPAfterBattle()
    {
        // Arrange
        var user = CreateTestUser();
        await _context.Users.AddAsync(user);

        var character = Character.Create("user1");
        await _context.Characters.AddAsync(character);
        await _context.SaveChangesAsync();

        _userManagerMock.Setup(m => m.FindByIdAsync("user1"))
            .ReturnsAsync(user);
        _userManagerMock.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        var combatResult = new Application.DTOs.CombatResult
        {
            Outcome = BattleOutcome.AttackerWon,
            Events = new List<Application.DTOs.CombatEvent>
            {
                new() { Type = "Victory", Winner = "Attacker", Timestamp = 0 }
            },
            AttackerFinalHP = 75, // Character took some damage during combat
            DefenderFinalHP = 0
        };

        _combatEngineMock.Setup(e => e.Simulate(It.IsAny<Character>(), It.IsAny<Character>(), It.IsAny<int>()))
            .Returns(combatResult);

        // Act
        var battle = await _stageService.ExecuteStageBattleAsync(character.Id);

        // Assert — HP should carry over from combat (continuous until defeat)
        var updatedCharacter = await _characterRepository.GetByIdAsync(character.Id);
        updatedCharacter!.CurrentHP.Should().Be(75, "HP should carry over from combat without healing between stages");
    }

    #endregion

    #region ReturnToCheckpointAsync Tests

    [Fact]
    public async Task ReturnToCheckpointAsync_ShouldResetToLastCheckpoint()
    {
        // Arrange
        var userId = "user1";
        var user = CreateTestUser(userId);
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var progress = StageProgress.Create(userId);

        // Advance to stage 25 (checkpoint is at 21 with every-10 checkpoints)
        for (int i = 1; i < 25; i++)
        {
            progress.AdvanceStage();
        }

        await _stageProgressRepository.AddAsync(progress);

        // Act
        var result = await _stageService.ReturnToCheckpointAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.CurrentStage.Should().Be(21); // Should reset to checkpoint at 21
        result.HighestStage.Should().Be(25); // Highest stage should remain
    }

    [Fact]
    public async Task ReturnToCheckpointAsync_WithNoProgress_ShouldThrow()
    {
        // Act
        var act = () => _stageService.ReturnToCheckpointAsync("nonexistent");

        // Assert
        await act.Should().ThrowAsync<Core.Exceptions.EntityNotFoundException>();
    }

    [Fact]
    public async Task ReturnToCheckpointAsync_ShouldUpdateRegion()
    {
        // Arrange
        var userId = "user1";
        var user = CreateTestUser(userId);
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var progress = StageProgress.Create(userId);

        // Set progress to stage 20015 directly (in the Arena — checkpoint falls back to 20001)
        progress.CurrentStage = 20015;
        progress.HighestStage = 20015;
        progress.LastCheckpoint = 20001;
        progress.CurrentRegion = RegionType.Arena;

        await _stageProgressRepository.AddAsync(progress);

        // Act
        var result = await _stageService.ReturnToCheckpointAsync(userId);

        // Assert
        result.CurrentStage.Should().Be(20001); // Arena has no new checkpoints after 20001
        result.CurrentRegion.Should().Be(RegionType.Arena); // Stage 20001+ is Arena region
    }

    #endregion

    #region StageProgress Entity Tests

    [Theory]
    [InlineData(1, 1)]
    [InlineData(5, 1)] // Stages 2-10 checkpoint at 1 (starting point)
    [InlineData(10, 1)] // Boss stage - checkpoint is still 1
    [InlineData(11, 11)] // After boss - new checkpoint starts
    [InlineData(15, 11)]
    [InlineData(20, 11)] // Boss stage - checkpoint is 11
    [InlineData(21, 21)] // After boss - new checkpoint
    [InlineData(25, 21)]
    [InlineData(50, 41)]
    [InlineData(99, 91)]
    [InlineData(100, 91)] // Boss stage (last Forest boss)
    [InlineData(101, 101)] // First Swamp stage
    [InlineData(150, 141)]
    [InlineData(200, 191)] // Boss stage (last Swamp boss)
    [InlineData(201, 201)] // First Mountains stage
    [InlineData(250, 241)]
    [InlineData(999, 991)]
    [InlineData(1000, 991)] // Boss stage (last Forest boss)
    [InlineData(1001, 1001)] // Swamp biome — still has checkpoints
    [InlineData(1050, 1041)]
    [InlineData(1100, 1091)] // Boss stage in Swamp
    [InlineData(1101, 1101)] // Continues Swamp
    [InlineData(1500, 1491)]
    [InlineData(2000, 1991)] // Boss stage (last Swamp boss)
    [InlineData(2001, 2001)] // Mountains — still has checkpoints
    [InlineData(5000, 4991)] // Tropical — checkpoints continue
    [InlineData(20000, 19991)] // Last Light boss
    [InlineData(20001, 20001)] // Arena — no new checkpoints
    [InlineData(25000, 20001)]
    public void CalculateCheckpoint_ShouldReturnCorrectCheckpoint(int stage, int expectedCheckpoint)
    {
        // Act
        var checkpoint = StageProgress.CalculateCheckpoint(stage);

        // Assert
        checkpoint.Should().Be(expectedCheckpoint);
    }

    [Theory]
    [InlineData(1, RegionType.Forest)]       // 1-1000
    [InlineData(500, RegionType.Forest)]
    [InlineData(1000, RegionType.Forest)]
    [InlineData(1001, RegionType.Swamp)]     // 1001-2000
    [InlineData(2000, RegionType.Swamp)]
    [InlineData(2001, RegionType.Mountains)] // 2001-3000
    [InlineData(3000, RegionType.Mountains)]
    [InlineData(3001, RegionType.Snowy)]     // 3001-4000
    [InlineData(4000, RegionType.Snowy)]
    [InlineData(4001, RegionType.Tropical)]  // 4001-5000
    [InlineData(5000, RegionType.Tropical)]
    [InlineData(5001, RegionType.Caverns)]   // 5001-6000
    [InlineData(6000, RegionType.Caverns)]
    [InlineData(6001, RegionType.Desert)]    // 6001-7000
    [InlineData(7000, RegionType.Desert)]
    [InlineData(7001, RegionType.Volcanic)]  // 7001-8000
    [InlineData(8000, RegionType.Volcanic)]
    [InlineData(8001, RegionType.Ruins)]     // 8001-9000
    [InlineData(9000, RegionType.Ruins)]
    [InlineData(9001, RegionType.Sky)]       // 9001-10000
    [InlineData(10000, RegionType.Sky)]
    [InlineData(10001, RegionType.Underwater)]  // 10001-11000
    [InlineData(10500, RegionType.Underwater)]
    [InlineData(11000, RegionType.Underwater)]
    [InlineData(11001, RegionType.Underground)] // 11001-12000
    [InlineData(14001, RegionType.Corruption)]  // 14001-15000
    [InlineData(16001, RegionType.Alien)]       // 16001-17000
    [InlineData(18001, RegionType.Timerift)]    // 18001-19000
    [InlineData(20000, RegionType.Light)]       // 19001-20000
    [InlineData(20001, RegionType.Arena)]       // 20001+
    [InlineData(25000, RegionType.Arena)]
    [InlineData(99999, RegionType.Arena)]
    public void GetRegionForStage_ShouldReturnCorrectRegion(int stage, RegionType expectedRegion)
    {
        // Act
        var region = StageProgress.GetRegionForStage(stage);

        // Assert
        region.Should().Be(expectedRegion);
    }

    [Theory]
    [InlineData(1, EnemyType.Normal)]
    [InlineData(5, EnemyType.Normal)]
    [InlineData(9, EnemyType.Normal)]
    [InlineData(10, EnemyType.MiniBoss)]  // MiniBoss every 10 (not 100)
    [InlineData(11, EnemyType.Normal)]
    [InlineData(20, EnemyType.MiniBoss)]
    [InlineData(50, EnemyType.MiniBoss)]
    [InlineData(99, EnemyType.Normal)]
    [InlineData(100, EnemyType.Boss)]     // Boss every 100
    [InlineData(110, EnemyType.MiniBoss)]
    [InlineData(200, EnemyType.Boss)]
    [InlineData(1000, EnemyType.Boss)]
    [InlineData(1001, EnemyType.Normal)]
    [InlineData(1005, EnemyType.Normal)]
    [InlineData(1010, EnemyType.MiniBoss)]
    public void GetEnemyTypeForStage_ShouldReturnCorrectType(int stage, EnemyType expectedType)
    {
        // Act
        var enemyType = StageProgress.GetEnemyTypeForStage(stage);

        // Assert
        enemyType.Should().Be(expectedType);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(100, false)]
    [InlineData(1000, false)]
    [InlineData(1001, false)]
    [InlineData(1100, false)]
    [InlineData(1101, false)]
    [InlineData(2000, false)]
    [InlineData(2001, false)]  // 2001 is Swamp, not Arena
    [InlineData(5000, false)]  // 5000 is Tropical, not Arena
    [InlineData(20000, false)] // Last Light stage
    [InlineData(20001, true)]  // Arena starts at 20001
    [InlineData(99999, true)]
    public void IsInVoid_ShouldReturnCorrectValue(int stage, bool expected)
    {
        // Arrange
        var progress = StageProgress.Create("user1");
        for (int i = 1; i < stage; i++)
        {
            progress.AdvanceStage();
        }

        // Act — IsInVoid() is now an alias for IsInArena()
        var result = progress.IsInVoid();

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void AdvanceStage_ShouldIncrementStageAndUpdateStats()
    {
        // Arrange
        var progress = StageProgress.Create("user1");
        var initialStage = progress.CurrentStage;

        // Act
        progress.AdvanceStage();

        // Assert
        progress.CurrentStage.Should().Be(initialStage + 1);
        progress.HighestStage.Should().Be(initialStage + 1);
        progress.TotalStagesCleared.Should().Be(1);
    }

    [Fact]
    public void AdvanceStage_ShouldUpdateCheckpoint()
    {
        // Arrange
        var progress = StageProgress.Create("user1");

        // Advance to stage 100 (boss stage)
        for (int i = 1; i < 100; i++)
        {
            progress.AdvanceStage();
        }

        // Act - advance to stage 101 (first stage after boss = new checkpoint)
        progress.AdvanceStage();

        // Assert
        progress.CurrentStage.Should().Be(101);
        progress.LastCheckpoint.Should().Be(101);
    }

    [Fact]
    public void AdvanceStage_ShouldUpdateRegion()
    {
        // Arrange
        var progress = StageProgress.Create("user1");

        // Set progress to stage 20000 directly (end of Light region)
        progress.CurrentStage = 20000;
        progress.HighestStage = 20000;

        // Act - advance to stage 20001 (Arena region)
        progress.AdvanceStage();

        // Assert
        progress.CurrentStage.Should().Be(20001);
        progress.CurrentRegion.Should().Be(RegionType.Arena);
    }

    [Fact]
    public void RecordBossDefeat_ShouldIncrementCounter()
    {
        // Arrange
        var progress = StageProgress.Create("user1");

        // Act
        progress.RecordBossDefeat();

        // Assert
        progress.TotalBossesDefeated.Should().Be(1);
    }

    [Fact]
    public void RecordBossDefeat_AtStage20000_ShouldUnlockEndlessMode()
    {
        // Arrange
        var progress = StageProgress.Create("user1");

        // Set to stage 20000 directly (last Light boss)
        progress.CurrentStage = 20000;
        progress.HighestStage = 20000;

        // Act
        progress.RecordBossDefeat();

        // Assert
        progress.EndlessModeUnlocked.Should().BeTrue();
    }

    #endregion

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
