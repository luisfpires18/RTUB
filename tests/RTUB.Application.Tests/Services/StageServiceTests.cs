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
                BaseEnemyStats = new BaseEnemyStats
                {
                    Normal = new EnemyTypeStat { Hp = 100, Power = 10, Speed = 10, Defense = 5, CriticalChance = 0.03 },
                    Boss = new EnemyTypeStat { Hp = 500, Power = 50, Speed = 20, Defense = 25, CriticalChance = 0.10 }
                },
                EnemyScaling = new EnemyScaling
                {
                    HpPerStage = 0.1,
                    PowerPerStage = 0.08,
                    SpeedPerStage = 0.02,
                    DefensePerStage = 0.04,
                    CriticalChancePerStage = 0.001
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
        _biomeServiceMock.Setup(x => x.CalculateScaledStats(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>()))
            .Returns((int stage, int hp, int damage, bool isBoss) => (hp, damage));

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
        // Stage 1: stageScaling = 1.0 + (1 * 0.05) = 1.05
        battle.XPReward.Should().Be(32); // round(BaseStageXP(30) * 1.05) = 32
        battle.FidelisReward.Should().Be(10.50m); // round(NormalWin(10) * 1.05, 2) = 10.50

        // Verify character XP was updated
        var updatedCharacter = await _characterRepository.GetByIdAsync(character.Id);
        updatedCharacter!.XP.Should().Be(initialXP + 32);

        // Verify user Fidelis was updated
        _userManagerMock.Verify(m => m.UpdateAsync(It.Is<ApplicationUser>(u =>
            u.FidelisBalance == 110.50m)), Times.Once);
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
        // Stage mode gives no rewards on defeat
        battle.XPReward.Should().Be(0);
        battle.FidelisReward.Should().Be(0m);
        battle.BeersDropped.Should().Be(0);
        battle.ShotsDropped.Should().Be(0);

        // Verify character XP was NOT updated (no consolation rewards)
        var updatedCharacter = await _characterRepository.GetByIdAsync(character.Id);
        updatedCharacter!.XP.Should().Be(initialXP);

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
        // BaseStageXP (30) * BossXPMultiplier (10) * stageScaling (1 + 100*0.05 = 6.0) = 1800
        battle.XPReward.Should().Be(1800);

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
        result.CurrentStage.Should().Be(21); // Should reset to checkpoint at 21 (after boss at 20)
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
        result.CurrentStage.Should().Be(101);
        result.CurrentRegion.Should().Be(RegionType.Desert); // Stage 101 is in Desert region
    }

    #endregion

    #region StageProgress Entity Tests

    [Theory]
    [InlineData(1, 1)]
    [InlineData(5, 1)] // Stages 2-10 checkpoint at 1 (starting point)
    [InlineData(10, 1)] // Boss stage - checkpoint is still 1 (before boss)
    [InlineData(11, 11)] // After boss - new checkpoint starts
    [InlineData(15, 11)]
    [InlineData(20, 11)] // Boss stage - checkpoint is 11
    [InlineData(21, 21)] // After boss - new checkpoint
    [InlineData(25, 21)]
    [InlineData(99, 91)]
    [InlineData(100, 91)] // Boss stage
    [InlineData(101, 101)] // After stage 100: every 20 stages
    [InlineData(120, 101)]
    [InlineData(121, 121)]
    [InlineData(135, 121)]
    [InlineData(140, 121)]
    [InlineData(141, 141)]
    [InlineData(999, 981)]
    [InlineData(10000, 9981)] // Last checkpoint before Infinite Land
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
    [InlineData(10, EnemyType.Boss)]
    [InlineData(20, EnemyType.Boss)]
    [InlineData(30, EnemyType.Boss)]
    [InlineData(90, EnemyType.Boss)]
    [InlineData(100, EnemyType.Boss)]
    [InlineData(200, EnemyType.Boss)]
    [InlineData(1000, EnemyType.Boss)]
    [InlineData(10000, EnemyType.Boss)]
    [InlineData(10001, EnemyType.Normal)]
    [InlineData(10010, EnemyType.Boss)]
    [InlineData(10100, EnemyType.Boss)]
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

        // Advance to stage 10 (boss stage)
        for (int i = 1; i < 10; i++)
        {
            progress.AdvanceStage();
        }

        // Act - advance to stage 11 (first stage after boss = new checkpoint)
        progress.AdvanceStage();

        // Assert
        progress.CurrentStage.Should().Be(11);
        progress.LastCheckpoint.Should().Be(11);
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

    #endregion

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
