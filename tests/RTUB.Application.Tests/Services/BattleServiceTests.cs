using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
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

        _battleService = new BattleService(
            _characterRepository,
            _battleRepository,
            _matchmakingServiceMock.Object,
            _combatEngineMock.Object,
            _inventoryRepositoryMock.Object,
            _userManagerMock.Object,
            _loggerMock.Object);
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

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
