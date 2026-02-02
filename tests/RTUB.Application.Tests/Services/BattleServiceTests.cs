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
/// Unit tests for BattleService
/// Tests battle creation, combat simulation, and reward distribution
/// </summary>
public class BattleServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly ICharacterRepository _characterRepository;
    private readonly IBattleRepository _battleRepository;
    private readonly Mock<IMatchmakingService> _matchmakingServiceMock;
    private readonly Mock<ICombatEngine> _combatEngineMock;
    private readonly Mock<IInventoryRepository> _inventoryRepositoryMock;
    private readonly Mock<ILogger<BattleService>> _loggerMock;
    private readonly Mock<IOptions<MyTunoScalingConfiguration>> _myTunoScalingConfigMock;
    private readonly IBattleService _battleService;

    public BattleServiceTests()
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

        _characterRepository = new CharacterRepository(_context);
        _battleRepository = new BattleRepository(_context);
        _matchmakingServiceMock = new Mock<IMatchmakingService>();
        _combatEngineMock = new Mock<ICombatEngine>();
        _inventoryRepositoryMock = new Mock<IInventoryRepository>();
        _loggerMock = new Mock<ILogger<BattleService>>();

        // Setup MyTunoScalingConfiguration mock with default values
        var myTunoScalingConfig = new MyTunoScalingConfiguration
        {
            BattleRewards = new BattleRewards
            {
                WinReward = 10m,
                LossReward = 5m,
                DrawReward = 7.5m
            }
        };
        _myTunoScalingConfigMock = new Mock<IOptions<MyTunoScalingConfiguration>>();
        _myTunoScalingConfigMock.Setup(x => x.Value).Returns(myTunoScalingConfig);

        _battleService = new BattleService(
            _characterRepository,
            _battleRepository,
            _matchmakingServiceMock.Object,
            _combatEngineMock.Object,
            _inventoryRepositoryMock.Object,
            _userManagerMock.Object,
            _loggerMock.Object,
            _myTunoScalingConfigMock.Object);
    }

    [Fact]
    public async Task CreateBattleVsAIAsync_WithValidInputs_ShouldCreateBattle()
    {
        // Arrange
        var user = new ApplicationUser { Id = "user1", UserName = "testuser", FidelisBalance = 100m };
        var playerCharacter = Character.Create("user1");
        var aiOpponent = Character.Create("user2");

        await _context.Characters.AddRangeAsync(playerCharacter, aiOpponent);
        await _context.SaveChangesAsync();

        _userManagerMock.Setup(m => m.FindByIdAsync("user1"))
            .ReturnsAsync(user);
        _userManagerMock.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        _matchmakingServiceMock.Setup(m => m.FindAIOpponentAsync(It.IsAny<Character>()))
            .ReturnsAsync(aiOpponent);

        var combatResult = new Application.DTOs.CombatResult
        {
            Outcome = BattleOutcome.AttackerWon,
            Events = new List<Application.DTOs.CombatEvent>
            {
                new() { Type = "RoundStart", Round = 1, Timestamp = 0 },
                new() { Type = "Attack", Attacker = "Attacker", Defender = "Defender", Damage = 10, Timestamp = 1 },
                new() { Type = "Victory", Winner = "Attacker", Timestamp = 2 }
            },
            AttackerFinalHP = 90,
            DefenderFinalHP = 0
        };

        _combatEngineMock.Setup(e => e.Simulate(It.IsAny<Character>(), It.IsAny<Character>(), It.IsAny<int>()))
            .Returns(combatResult);

        // Act
        var battle = await _battleService.CreateBattleVsAIAsync(playerCharacter.Id);

        // Assert
        battle.Should().NotBeNull();
        battle.AttackerCharacterId.Should().Be(playerCharacter.Id);
        battle.DefenderCharacterId.Should().Be(aiOpponent.Id);
        battle.Outcome.Should().Be(BattleOutcome.AttackerWon);
        battle.AttackerXP.Should().BeGreaterThan(0);
        battle.AttackerFidelis.Should().BeGreaterThan(0);
        battle.DefenderXP.Should().Be(0); // AI doesn't get rewards
        battle.DefenderFidelis.Should().Be(0m); // AI doesn't get rewards
        battle.ReplayJson.Should().NotBeNullOrEmpty();

        // Verify battle was persisted
        var savedBattle = await _battleRepository.GetByIdAsync(battle.Id);
        savedBattle.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateBattleVsAIAsync_WithInvalidCharacterId_ShouldThrowException()
    {
        // Act
        var act = () => _battleService.CreateBattleVsAIAsync(999);

        // Assert
        await act.Should().ThrowAsync<Core.Exceptions.EntityNotFoundException>();
    }

    [Fact]
    public async Task CreateBattleVsAIAsync_WithNoAIOpponent_ShouldThrowException()
    {
        // Arrange
        var playerCharacter = Character.Create("user1");
        await _context.Characters.AddAsync(playerCharacter);
        await _context.SaveChangesAsync();

        _matchmakingServiceMock.Setup(m => m.FindAIOpponentAsync(It.IsAny<Character>()))
            .ReturnsAsync((Character?)null);

        // Act
        var act = () => _battleService.CreateBattleVsAIAsync(playerCharacter.Id);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Nenhum oponente AI disponível*");
    }

    [Fact]
    public async Task CreateBattleVsAIAsync_WithWin_ShouldApplyWinRewards()
    {
        // Arrange
        var user = new ApplicationUser { Id = "user1", UserName = "testuser", FidelisBalance = 100m };
        var playerCharacter = Character.Create("user1");
        var aiOpponent = Character.Create("user2");

        await _context.Characters.AddRangeAsync(playerCharacter, aiOpponent);
        await _context.SaveChangesAsync();

        _userManagerMock.Setup(m => m.FindByIdAsync("user1"))
            .ReturnsAsync(user);
        _userManagerMock.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        _matchmakingServiceMock.Setup(m => m.FindAIOpponentAsync(It.IsAny<Character>()))
            .ReturnsAsync(aiOpponent);

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

        var initialXP = playerCharacter.XP;
        var initialFidelis = user.FidelisBalance;

        // Act
        var battle = await _battleService.CreateBattleVsAIAsync(playerCharacter.Id);

        // Assert
        battle.AttackerXP.Should().Be(50); // BaseWinXP
        battle.AttackerFidelis.Should().Be(10m); // BaseWinFidelis

        // Verify character XP was updated
        var updatedCharacter = await _characterRepository.GetByIdAsync(playerCharacter.Id);
        updatedCharacter!.XP.Should().BeGreaterThan(initialXP);

        // Verify user Fidelis was updated
        _userManagerMock.Verify(m => m.UpdateAsync(It.Is<ApplicationUser>(u =>
            u.FidelisBalance == initialFidelis + 10m)), Times.Once);
    }

    [Fact]
    public async Task CreateBattleVsAIAsync_WithLoss_ShouldApplyLossRewards()
    {
        // Arrange
        var user = new ApplicationUser { Id = "user1", UserName = "testuser", FidelisBalance = 100m };
        var playerCharacter = Character.Create("user1");
        var aiOpponent = Character.Create("user2");

        await _context.Characters.AddRangeAsync(playerCharacter, aiOpponent);
        await _context.SaveChangesAsync();

        _userManagerMock.Setup(m => m.FindByIdAsync("user1"))
            .ReturnsAsync(user);
        _userManagerMock.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        _matchmakingServiceMock.Setup(m => m.FindAIOpponentAsync(It.IsAny<Character>()))
            .ReturnsAsync(aiOpponent);

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

        var initialXP = playerCharacter.XP;
        var initialFidelis = user.FidelisBalance;

        // Act
        var battle = await _battleService.CreateBattleVsAIAsync(playerCharacter.Id);

        // Assert
        battle.AttackerXP.Should().Be(20); // BaseLossXP
        battle.AttackerFidelis.Should().Be(5m); // BaseLossFidelis

        // Verify character XP was updated
        var updatedCharacter = await _characterRepository.GetByIdAsync(playerCharacter.Id);
        updatedCharacter!.XP.Should().BeGreaterThan(initialXP);

        // Verify user Fidelis was updated
        _userManagerMock.Verify(m => m.UpdateAsync(It.Is<ApplicationUser>(u =>
            u.FidelisBalance == initialFidelis + 5m)), Times.Once);
    }

    [Fact]
    public async Task CreateBattleVsAIAsync_WithDraw_ShouldApplyDrawRewards()
    {
        // Arrange
        var user = new ApplicationUser { Id = "user1", UserName = "testuser", FidelisBalance = 100m };
        var playerCharacter = Character.Create("user1");
        var aiOpponent = Character.Create("user2");

        await _context.Characters.AddRangeAsync(playerCharacter, aiOpponent);
        await _context.SaveChangesAsync();

        _userManagerMock.Setup(m => m.FindByIdAsync("user1"))
            .ReturnsAsync(user);
        _userManagerMock.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        _matchmakingServiceMock.Setup(m => m.FindAIOpponentAsync(It.IsAny<Character>()))
            .ReturnsAsync(aiOpponent);

        var combatResult = new Application.DTOs.CombatResult
        {
            Outcome = BattleOutcome.Draw,
            Events = new List<Application.DTOs.CombatEvent>
            {
                new() { Type = "Draw", Timestamp = 0 }
            },
            AttackerFinalHP = 10,
            DefenderFinalHP = 10
        };

        _combatEngineMock.Setup(e => e.Simulate(It.IsAny<Character>(), It.IsAny<Character>(), It.IsAny<int>()))
            .Returns(combatResult);

        var initialXP = playerCharacter.XP;
        var initialFidelis = user.FidelisBalance;

        // Act
        var battle = await _battleService.CreateBattleVsAIAsync(playerCharacter.Id);

        // Assert
        battle.AttackerXP.Should().Be(30); // BaseDrawXP
        battle.AttackerFidelis.Should().Be(7.5m); // BaseDrawFidelis

        // Verify character XP was updated
        var updatedCharacter = await _characterRepository.GetByIdAsync(playerCharacter.Id);
        updatedCharacter!.XP.Should().BeGreaterThan(initialXP);

        // Verify user Fidelis was updated
        _userManagerMock.Verify(m => m.UpdateAsync(It.Is<ApplicationUser>(u =>
            u.FidelisBalance == initialFidelis + 7.5m)), Times.Once);
    }

    [Fact]
    public async Task CreateBattleVsAIAsync_ShouldNotApplyRewardsToAI()
    {
        // Arrange
        var user = new ApplicationUser { Id = "user1", UserName = "testuser", FidelisBalance = 100m };
        var playerCharacter = Character.Create("user1");
        var aiOpponent = Character.Create("user2");

        await _context.Characters.AddRangeAsync(playerCharacter, aiOpponent);
        await _context.SaveChangesAsync();

        _userManagerMock.Setup(m => m.FindByIdAsync("user1"))
            .ReturnsAsync(user);
        _userManagerMock.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        _matchmakingServiceMock.Setup(m => m.FindAIOpponentAsync(It.IsAny<Character>()))
            .ReturnsAsync(aiOpponent);

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
        var battle = await _battleService.CreateBattleVsAIAsync(playerCharacter.Id);

        // Assert
        battle.DefenderXP.Should().Be(0); // AI doesn't get XP
        battle.DefenderFidelis.Should().Be(0m); // AI doesn't get Fidelis

        // Verify AI opponent character was not updated
        var aiCharacter = await _characterRepository.GetByIdAsync(aiOpponent.Id);
        aiCharacter!.XP.Should().Be(0); // Should remain unchanged
    }

    [Fact]
    public async Task CreateBattleVsOpponentAsync_WithReducedHP_ShouldIncludeMaxHPInEvents()
    {
        // Arrange - Test case: attacker with 73/130 HP should see MaxHP correctly in battle events
        var user1 = new ApplicationUser { Id = "user1", UserName = "player1", FidelisBalance = 100m };
        var user2 = new ApplicationUser { Id = "user2", UserName = "player2", FidelisBalance = 100m };
        
        var playerCharacter = Character.Create("user1");
        playerCharacter.Level = 5; // Level 5 gives TotalHP = 130
        playerCharacter.CurrentHP = 73; // Player starts battle with reduced HP
        
        var opponentCharacter = Character.Create("user2");
        opponentCharacter.Level = 5; // Same level
        opponentCharacter.CurrentHP = 100; // Opponent has some HP

        await _context.Characters.AddRangeAsync(playerCharacter, opponentCharacter);
        await _context.SaveChangesAsync();

        _userManagerMock.Setup(m => m.FindByIdAsync("user1")).ReturnsAsync(user1);
        _userManagerMock.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);

        // Setup combat result with proper HP initialization events
        var combatResult = new Application.DTOs.CombatResult
        {
            Outcome = BattleOutcome.AttackerWon,
            Events = new List<Application.DTOs.CombatEvent>
            {
                // Initial HP events should include MaxHP
                new() { Type = "HPUpdate", Character = "Attacker", HP = 73, MaxHP = 130, Timestamp = 0 },
                new() { Type = "HPUpdate", Character = "Defender", HP = 100, MaxHP = 130, Timestamp = 1 },
                new() { Type = "RoundStart", Round = 1, Timestamp = 2 },
                new() { Type = "Attack", Attacker = "Attacker", Defender = "Defender", Damage = 50, Timestamp = 3 },
                new() { Type = "HPUpdate", Character = "Defender", HP = 50, Timestamp = 4 },
                new() { Type = "Attack", Attacker = "Defender", Defender = "Attacker", Damage = 10, Timestamp = 5 },
                new() { Type = "HPUpdate", Character = "Attacker", HP = 63, Timestamp = 6 },
                new() { Type = "Victory", Winner = "Attacker", Timestamp = 7 }
            },
            AttackerFinalHP = 63,
            DefenderFinalHP = 0
        };

        _combatEngineMock.Setup(e => e.Simulate(It.IsAny<Character>(), It.IsAny<Character>(), It.IsAny<int>()))
            .Returns(combatResult);

        // Act
        var battle = await _battleService.CreateBattleVsOpponentAsync(playerCharacter.Id, opponentCharacter.Id);

        // Assert
        battle.Should().NotBeNull();
        battle.ReplayJson.Should().NotBeNullOrEmpty();
        
        // Parse the replay JSON to verify MaxHP is included
        var events = System.Text.Json.JsonSerializer.Deserialize<List<Application.DTOs.CombatEvent>>(battle.ReplayJson!);
        events.Should().NotBeNull();
        
        // First event should be attacker HPUpdate with MaxHP
        var attackerInitialHP = events![0];
        attackerInitialHP.Type.Should().Be("HPUpdate");
        attackerInitialHP.Character.Should().Be("Attacker");
        attackerInitialHP.HP.Should().Be(73, "because attacker starts with reduced HP");
        attackerInitialHP.MaxHP.Should().Be(130, "because MaxHP should reflect the attacker's total HP, not current HP");
        
        // Second event should be defender HPUpdate with MaxHP
        var defenderInitialHP = events[1];
        defenderInitialHP.Type.Should().Be("HPUpdate");
        defenderInitialHP.Character.Should().Be("Defender");
        defenderInitialHP.HP.Should().Be(100);
        defenderInitialHP.MaxHP.Should().Be(130);
    }
    
    [Fact]
    public async Task CreateBattleVsOpponentAsync_WithHigherLevelOpponent_ShouldGiveMoreXP()
    {
        // Arrange
        var user = new ApplicationUser { Id = "user1", UserName = "testuser", FidelisBalance = 100m };
        var playerCharacter = Character.Create("user1");
        playerCharacter.AddXP(0); // Level 1
        
        var opponent = Character.Create("user2");
        // Level up opponent to level 10
        for (int i = 0; i < 9; i++)
        {
            opponent.AddXP(opponent.Level * 100);
        }
        
        await _context.Characters.AddRangeAsync(playerCharacter, opponent);
        await _context.SaveChangesAsync();
        
        var initialPlayerXP = playerCharacter.XP;
        
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
        var battle = await _battleService.CreateBattleVsOpponentAsync(playerCharacter.Id, opponent.Id);
        
        // Assert
        // With level difference of 9 (defender level 10 - attacker level 1)
        // and scaling factor of 0.05, multiplier = 1.0 + (9 * 0.05) = 1.45
        // Expected XP = 50 * 1.45 = 72.5, rounded to 72
        battle.AttackerXP.Should().BeInRange(70, 75, "because level 10 vs level 1 with 5% scaling should give ~72 XP");
        
        // Verify XP was applied to character
        var updatedCharacter = await _characterRepository.GetByIdAsync(playerCharacter.Id);
        updatedCharacter!.XP.Should().Be(initialPlayerXP + battle.AttackerXP);
    }
    
    [Fact]
    public async Task CreateBattleVsOpponentAsync_WithLowerLevelOpponent_ShouldGiveLessXP()
    {
        // Arrange
        var user = new ApplicationUser { Id = "user1", UserName = "testuser", FidelisBalance = 100m };
        var playerCharacter = Character.Create("user1");
        // Level up player to level 10
        for (int i = 0; i < 9; i++)
        {
            playerCharacter.AddXP(playerCharacter.Level * 100);
        }
        
        var opponent = Character.Create("user2");
        opponent.AddXP(0); // Level 1
        
        await _context.Characters.AddRangeAsync(playerCharacter, opponent);
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
        var battle = await _battleService.CreateBattleVsOpponentAsync(playerCharacter.Id, opponent.Id);
        
        // Assert
        // With level difference of -9 (defender level 1 - attacker level 10)
        // and scaling factor of 0.05, multiplier = 1.0 + (-9 * 0.05) = 0.55
        // Expected XP = 50 * 0.55 = 27.5, rounded to 28
        battle.AttackerXP.Should().BeInRange(25, 30, "because level 10 vs level 1 with 5% scaling should give ~28 XP");
        battle.AttackerXP.Should().BeLessThan(50, "and it should be less than base XP");
        battle.AttackerXP.Should().BeGreaterThan(0, "but should still give some XP");
    }
    
    [Fact]
    public async Task CreateBattleVsOpponentAsync_WithSameLevelOpponent_ShouldGiveBaseXP()
    {
        // Arrange
        var user = new ApplicationUser { Id = "user1", UserName = "testuser", FidelisBalance = 100m };
        var playerCharacter = Character.Create("user1");
        var opponent = Character.Create("user2");
        
        await _context.Characters.AddRangeAsync(playerCharacter, opponent);
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
            AttackerFinalHP = 70,
            DefenderFinalHP = 0
        };
        
        _combatEngineMock.Setup(e => e.Simulate(It.IsAny<Character>(), It.IsAny<Character>(), It.IsAny<int>()))
            .Returns(combatResult);
        
        // Act
        var battle = await _battleService.CreateBattleVsOpponentAsync(playerCharacter.Id, opponent.Id);
        
        // Assert
        battle.AttackerXP.Should().Be(50, "because fighting an equal level opponent should give base XP (50 for win)");
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
