using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
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
    private readonly IStageBattleRepository _stageBattleRepository;
    private readonly IStageEnemyRepository _stageEnemyRepository;
    private readonly ICharacterRepository _characterRepository;
    private readonly Mock<ICombatEngine> _combatEngineMock;
    private readonly Mock<IInventoryRepository> _inventoryRepositoryMock;
    private readonly Mock<ILogger<StageService>> _loggerMock;
    private readonly Mock<IOptions<MyTunoScalingConfiguration>> _myTunoScalingConfigMock;
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
        _stageBattleRepository = new StageBattleRepository(_context);
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
            }
        };
        _myTunoScalingConfigMock = new Mock<IOptions<MyTunoScalingConfiguration>>();
        _myTunoScalingConfigMock.Setup(x => x.Value).Returns(myTunoScalingConfig);

        _stageService = new StageService(
            _stageProgressRepository,
            _stageBattleRepository,
            _stageEnemyRepository,
            _characterRepository,
            _combatEngineMock.Object,
            _inventoryRepositoryMock.Object,
            _userManagerMock.Object,
            _loggerMock.Object,
            _myTunoScalingConfigMock.Object);
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
        progress.TotalMiniBossesDefeated.Should().Be(0);
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

        // Assert
        battle.Should().NotBeNull();
        battle.CharacterId.Should().Be(character.Id);
        battle.StageNumber.Should().Be(1);
        battle.EnemyType.Should().Be(EnemyType.Normal);
        battle.Region.Should().Be(RegionType.Forest);
        battle.Outcome.Should().Be(BattleOutcome.AttackerWon);
        battle.XPReward.Should().BeGreaterThan(0);
        battle.FidelisReward.Should().BeGreaterThan(0);
        battle.ReplayJson.Should().NotBeNullOrEmpty();

        // Verify battle was persisted
        var savedBattle = await _stageBattleRepository.GetByIdAsync(battle.Id);
        savedBattle.Should().NotBeNull();
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
    public async Task ExecuteStageBattleAsync_WithDeadCharacter_ShouldThrow()
    {
        // Arrange
        var character = Character.Create("user1");
        character.CurrentHP = 0; // Dead character
        await _context.Characters.AddAsync(character);
        await _context.SaveChangesAsync();

        // Act
        var act = () => _stageService.ExecuteStageBattleAsync(character.Id);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Personagem derrotado*");
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
        battle.XPReward.Should().Be(30); // BaseStageXP for normal enemy
        battle.FidelisReward.Should().Be(10m); // Win reward from config

        // Verify character XP was updated
        var updatedCharacter = await _characterRepository.GetByIdAsync(character.Id);
        updatedCharacter!.XP.Should().Be(initialXP + 30);

        // Verify user Fidelis was updated
        _userManagerMock.Verify(m => m.UpdateAsync(It.Is<ApplicationUser>(u =>
            u.FidelisBalance == 110m)), Times.Once);
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
        battle.XPReward.Should().Be(10); // BaseStageXP / 3 for loss
        battle.FidelisReward.Should().Be(5m); // Loss reward from config
        battle.BeersDropped.Should().Be(0); // No drops on loss
        battle.ShotsDropped.Should().Be(0); // No drops on loss

        // Verify character XP was updated
        var updatedCharacter = await _characterRepository.GetByIdAsync(character.Id);
        updatedCharacter!.XP.Should().Be(initialXP + 10);

        // Verify user Fidelis was updated
        _userManagerMock.Verify(m => m.UpdateAsync(It.Is<ApplicationUser>(u =>
            u.FidelisBalance == 105m)), Times.Once);
    }

    [Fact]
    public async Task ExecuteStageBattleAsync_WhenFightingMiniBoss_ShouldGiveTripleXP()
    {
        // Arrange
        var user = CreateTestUser();
        await _context.Users.AddAsync(user);

        var character = Character.Create("user1");
        await _context.Characters.AddAsync(character);
        await _context.SaveChangesAsync();

        // Create progress at stage 10 (mini-boss stage)
        var progress = StageProgress.Create("user1");
        for (int i = 1; i < 10; i++)
        {
            progress.AdvanceStage();
        }
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
        battle.EnemyType.Should().Be(EnemyType.MiniBoss);
        battle.XPReward.Should().Be(90); // BaseStageXP (30) * MiniBossXPMultiplier (3)

        // Verify mini-boss defeat was recorded
        var updatedProgress = await _stageProgressRepository.GetByUserIdAsync("user1");
        updatedProgress!.TotalMiniBossesDefeated.Should().Be(1);
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

        // Create progress at stage 100 (boss stage)
        var progress = StageProgress.Create("user1");
        for (int i = 1; i < 100; i++)
        {
            progress.AdvanceStage();
        }
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
        battle.XPReward.Should().Be(300); // BaseStageXP (30) * BossXPMultiplier (10)

        // Verify boss defeat was recorded
        var updatedProgress = await _stageProgressRepository.GetByUserIdAsync("user1");
        updatedProgress!.TotalBossesDefeated.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteStageBattleAsync_WhenFightingBossAtStage10000_ShouldUnlockEndlessMode()
    {
        // Arrange
        var user = CreateTestUser();
        await _context.Users.AddAsync(user);

        var character = Character.Create("user1");
        await _context.Characters.AddAsync(character);
        await _context.SaveChangesAsync();

        // Create progress at stage 10000 (final boss)
        var progress = StageProgress.Create("user1");
        for (int i = 1; i < 10000; i++)
        {
            progress.AdvanceStage();
        }
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
        battle.StageNumber.Should().Be(10000);

        // Verify endless mode was unlocked
        var updatedProgress = await _stageProgressRepository.GetByUserIdAsync("user1");
        updatedProgress!.EndlessModeUnlocked.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteStageBattleAsync_ShouldUpdateCharacterHP()
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
            AttackerFinalHP = 75, // Character took some damage
            DefenderFinalHP = 0
        };

        _combatEngineMock.Setup(e => e.Simulate(It.IsAny<Character>(), It.IsAny<Character>(), It.IsAny<int>()))
            .Returns(combatResult);

        // Act
        var battle = await _stageService.ExecuteStageBattleAsync(character.Id);

        // Assert
        var updatedCharacter = await _characterRepository.GetByIdAsync(character.Id);
        updatedCharacter!.CurrentHP.Should().Be(75);
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

        // Advance to stage 25 (checkpoint would be at 20)
        for (int i = 1; i < 25; i++)
        {
            progress.AdvanceStage();
        }

        await _stageProgressRepository.AddAsync(progress);

        // Act
        var result = await _stageService.ReturnToCheckpointAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.CurrentStage.Should().Be(20); // Should reset to checkpoint at 20
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

        // Advance to stage 115 (checkpoint would be at 100, which is in Forest region)
        for (int i = 1; i < 115; i++)
        {
            progress.AdvanceStage();
        }

        await _stageProgressRepository.AddAsync(progress);

        // Act
        var result = await _stageService.ReturnToCheckpointAsync(userId);

        // Assert
        result.CurrentStage.Should().Be(100);
        result.CurrentRegion.Should().Be(RegionType.Forest); // Stage 100 is in Forest
    }

    #endregion

    #region GetRecentBattlesAsync Tests

    [Fact]
    public async Task GetRecentBattlesAsync_ShouldReturnBattles()
    {
        // Arrange
        var user = CreateTestUser();
        await _context.Users.AddAsync(user);

        var character = Character.Create("user1");
        await _context.Characters.AddAsync(character);
        await _context.SaveChangesAsync();

        // Create some battles
        var battle1 = StageBattle.Create(character.Id, 1, null, EnemyType.Normal, RegionType.Forest, "Enemy 1", 123, BattleOutcome.AttackerWon);
        battle1.SetReplay("{}");
        battle1.SetRewards(30, 10m);

        var battle2 = StageBattle.Create(character.Id, 2, null, EnemyType.Normal, RegionType.Forest, "Enemy 2", 456, BattleOutcome.AttackerWon);
        battle2.SetReplay("{}");
        battle2.SetRewards(30, 10m);

        await _context.StageBattles.AddRangeAsync(battle1, battle2);
        await _context.SaveChangesAsync();

        // Act
        var battles = await _stageService.GetRecentBattlesAsync(character.Id);

        // Assert
        battles.Should().NotBeNull();
        battles.Should().HaveCount(2);
        battles.Should().ContainEquivalentOf(battle1);
        battles.Should().ContainEquivalentOf(battle2);
    }

    [Fact]
    public async Task GetRecentBattlesAsync_WithLimit_ShouldRespectLimit()
    {
        // Arrange
        var user = CreateTestUser();
        await _context.Users.AddAsync(user);

        var character = Character.Create("user1");
        await _context.Characters.AddAsync(character);
        await _context.SaveChangesAsync();

        // Create 15 battles
        for (int i = 1; i <= 15; i++)
        {
            var battle = StageBattle.Create(character.Id, i, null, EnemyType.Normal, RegionType.Forest, $"Enemy {i}", i, BattleOutcome.AttackerWon);
            battle.SetReplay("{}");
            battle.SetRewards(30, 10m);
            await _context.StageBattles.AddAsync(battle);
        }
        await _context.SaveChangesAsync();

        // Act
        var battles = await _stageService.GetRecentBattlesAsync(character.Id, 5);

        // Assert
        battles.Should().HaveCount(5);
    }

    #endregion

    #region StageProgress Entity Tests

    [Theory]
    [InlineData(1, 1)]
    [InlineData(5, 1)] // Stages 2-9 checkpoint at 1 (starting point)
    [InlineData(10, 10)]
    [InlineData(15, 10)]
    [InlineData(20, 20)]
    [InlineData(99, 90)]
    [InlineData(100, 100)]
    [InlineData(101, 100)]
    [InlineData(120, 120)]
    [InlineData(135, 120)]
    [InlineData(140, 140)]
    [InlineData(999, 980)]
    [InlineData(10000, 10000)]
    [InlineData(10001, 10000)]
    [InlineData(15000, 10000)]
    public void CalculateCheckpoint_ShouldReturnCorrectCheckpoint(int stage, int expectedCheckpoint)
    {
        // Act
        var checkpoint = StageProgress.CalculateCheckpoint(stage);

        // Assert
        checkpoint.Should().Be(expectedCheckpoint);
    }

    [Theory]
    [InlineData(1, RegionType.Forest)]
    [InlineData(50, RegionType.Forest)]
    [InlineData(100, RegionType.Forest)]
    [InlineData(101, RegionType.Desert)]
    [InlineData(200, RegionType.Desert)]
    [InlineData(201, RegionType.Mountains)]
    [InlineData(300, RegionType.Mountains)]
    [InlineData(1000, RegionType.CursedLands)]
    [InlineData(1001, RegionType.Forest)] // Cycles back
    [InlineData(1100, RegionType.Forest)]
    [InlineData(10000, RegionType.CursedLands)]
    [InlineData(10001, RegionType.InfiniteLand)]
    [InlineData(15000, RegionType.InfiniteLand)]
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
    [InlineData(10, EnemyType.MiniBoss)]
    [InlineData(20, EnemyType.MiniBoss)]
    [InlineData(30, EnemyType.MiniBoss)]
    [InlineData(90, EnemyType.MiniBoss)]
    [InlineData(100, EnemyType.Boss)]
    [InlineData(200, EnemyType.Boss)]
    [InlineData(1000, EnemyType.Boss)]
    [InlineData(10000, EnemyType.Boss)]
    [InlineData(10001, EnemyType.Normal)] // No mini-bosses in Infinite Land
    [InlineData(10010, EnemyType.Normal)] // No mini-bosses in Infinite Land
    [InlineData(10100, EnemyType.Boss)] // Bosses still appear every 100
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
    [InlineData(10000, false)]
    [InlineData(10001, true)]
    [InlineData(15000, true)]
    [InlineData(99999, true)]
    public void IsInInfiniteLand_ShouldReturnCorrectValue(int stage, bool expected)
    {
        // Arrange
        var progress = StageProgress.Create("user1");
        for (int i = 1; i < stage; i++)
        {
            progress.AdvanceStage();
        }

        // Act
        var result = progress.IsInInfiniteLand();

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

        // Advance to stage 9
        for (int i = 1; i < 9; i++)
        {
            progress.AdvanceStage();
        }

        // Act - advance to stage 10 (checkpoint)
        progress.AdvanceStage();

        // Assert
        progress.CurrentStage.Should().Be(10);
        progress.LastCheckpoint.Should().Be(10);
    }

    [Fact]
    public void AdvanceStage_ShouldUpdateRegion()
    {
        // Arrange
        var progress = StageProgress.Create("user1");

        // Advance to stage 100 (end of Forest)
        for (int i = 1; i < 100; i++)
        {
            progress.AdvanceStage();
        }

        // Act - advance to stage 101 (Desert region)
        progress.AdvanceStage();

        // Assert
        progress.CurrentStage.Should().Be(101);
        progress.CurrentRegion.Should().Be(RegionType.Desert);
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
    public void RecordBossDefeat_AtStage10000_ShouldUnlockEndlessMode()
    {
        // Arrange
        var progress = StageProgress.Create("user1");

        // Advance to stage 10000
        for (int i = 1; i < 10000; i++)
        {
            progress.AdvanceStage();
        }

        // Act
        progress.RecordBossDefeat();

        // Assert
        progress.EndlessModeUnlocked.Should().BeTrue();
    }

    [Fact]
    public void RecordMiniBossDefeat_ShouldIncrementCounter()
    {
        // Arrange
        var progress = StageProgress.Create("user1");

        // Act
        progress.RecordMiniBossDefeat();

        // Assert
        progress.TotalMiniBossesDefeated.Should().Be(1);
    }

    #endregion

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
