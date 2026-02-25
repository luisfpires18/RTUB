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
/// Unit tests for BattleService
/// Tests battle creation, combat simulation, and reward distribution
/// </summary>
public class BattleServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DbContextOptions<ApplicationDbContext> _dbOptions;
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly ICharacterRepository _characterRepository;
    private readonly Mock<ICombatEngine> _combatEngineMock;
    private readonly Mock<IInventoryRepository> _inventoryRepositoryMock;
    private readonly Mock<ILogger<BattleService>> _loggerMock;
    private readonly Mock<IOptions<MyTunoScalingConfiguration>> _myTunoScalingConfigMock;
    private readonly IBattleService _battleService;

    public BattleServiceTests()
    {
        _dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(
            _dbOptions,
            Mock.Of<IHttpContextAccessor>(),
            new AuditContext(),
            new AuditLogAppender());

        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _characterRepository = new CharacterRepository(CreateContextFactory());
        _combatEngineMock = new Mock<ICombatEngine>();
        _inventoryRepositoryMock = new Mock<IInventoryRepository>();
        _loggerMock = new Mock<ILogger<BattleService>>();

        // Setup MyTunoScalingConfiguration mock with default values
        var myTunoScalingConfig = new MyTunoScalingConfiguration
        {
            BattleRewards = new BattleRewards()
        };
        _myTunoScalingConfigMock = new Mock<IOptions<MyTunoScalingConfiguration>>();
        _myTunoScalingConfigMock.Setup(x => x.Value).Returns(myTunoScalingConfig);

        _battleService = new BattleService(
            _characterRepository,
            _combatEngineMock.Object,
            _inventoryRepositoryMock.Object,
            _userManagerMock.Object,
            _loggerMock.Object,
            _myTunoScalingConfigMock.Object,
            Mock.Of<IAuditLogService>(),
            Mock.Of<IStageProgressRepository>(),
            CreateContextFactory());
    }

    [Fact]
    public async Task CreateBattleVsOpponentAsync_WithValidInputs_ShouldCreateBattle()
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
        var battle = await _battleService.CreateBattleVsOpponentAsync(playerCharacter.Id, opponent.Id);

        // Assert - BattleResult is returned but not persisted
        battle.Should().NotBeNull();
        battle.BattleId.Should().NotBe(Guid.Empty);
        battle.AttackerCharacterId.Should().Be(playerCharacter.Id);
        battle.DefenderCharacterId.Should().Be(opponent.Id);
        battle.Outcome.Should().Be(BattleOutcome.AttackerWon);
        battle.AttackerXP.Should().BeGreaterThan(0);
        battle.AttackerFidelis.Should().BeGreaterThan(0);
        battle.ReplayJson.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreateBattleVsOpponentAsync_WithInvalidCharacterId_ShouldThrowException()
    {
        // Arrange
        var opponent = Character.Create("user2");
        await _context.Characters.AddAsync(opponent);
        await _context.SaveChangesAsync();

        // Act
        var act = () => _battleService.CreateBattleVsOpponentAsync(999, opponent.Id);

        // Assert
        await act.Should().ThrowAsync<Core.Exceptions.EntityNotFoundException>();
    }

    [Fact]
    public async Task CreateBattleVsOpponentAsync_WithInvalidOpponentId_ShouldThrowException()
    {
        // Arrange
        var playerCharacter = Character.Create("user1");
        await _context.Characters.AddAsync(playerCharacter);
        await _context.SaveChangesAsync();

        // Act
        var act = () => _battleService.CreateBattleVsOpponentAsync(playerCharacter.Id, 999);

        // Assert
        await act.Should().ThrowAsync<Core.Exceptions.EntityNotFoundException>();
    }

    [Fact]
    public async Task CreateBattleVsOpponentAsync_WithWin_ShouldApplyWinRewards()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "user1", UserName = "testuser", FidelisBalance = 100m,
            NormalizedUserName = "TESTUSER", Email = "t@t.com", NormalizedEmail = "T@T.COM",
            SecurityStamp = Guid.NewGuid().ToString(), ConcurrencyStamp = Guid.NewGuid().ToString(),
            FirstName = "Test", LastName = "User", Nickname = "tester", PhoneNumber = "000000000"
        };
        var playerCharacter = Character.Create("user1");
        var opponent = Character.Create("user2");

        _context.Users.Add(user);
        await _context.Characters.AddRangeAsync(playerCharacter, opponent);
        await _context.SaveChangesAsync();

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
        var battle = await _battleService.CreateBattleVsOpponentAsync(playerCharacter.Id, opponent.Id);
        await _battleService.FinalizeAndApplyRewardsAsync(battle);

        // Assert
        // Level 1 vs Level 1: levelDiff = 0, levelDiffMult = 1.0
        // XP = round(100 * 1.0) = 100
        battle.AttackerXP.Should().Be(100);
        // Fidelis = round(120 * 1.0, 2) = 120.00
        battle.AttackerFidelis.Should().Be(120.00m);

        // Verify character gained XP (may have leveled up, resetting XP to 0)
        var updatedCharacter = await _characterRepository.GetByIdAsync(playerCharacter.Id);
        (updatedCharacter!.Level > 1 || updatedCharacter.XP > initialXP).Should().BeTrue(
            "character should have gained XP or leveled up");

        // Verify user Fidelis was updated via DbContext
        var updatedUser = await _context.Users.FindAsync("user1");
        updatedUser!.FidelisBalance.Should().Be(initialFidelis + 120.00m);
    }

    [Fact]
    public async Task CreateBattleVsOpponentAsync_WithLoss_ShouldNotAwardRewards()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "user1", UserName = "testuser", FidelisBalance = 100m,
            NormalizedUserName = "TESTUSER", Email = "t@t.com", NormalizedEmail = "T@T.COM",
            SecurityStamp = Guid.NewGuid().ToString(), ConcurrencyStamp = Guid.NewGuid().ToString(),
            FirstName = "Test", LastName = "User", Nickname = "tester", PhoneNumber = "000000000"
        };
        var playerCharacter = Character.Create("user1");
        var opponent = Character.Create("user2");

        _context.Users.Add(user);
        await _context.Characters.AddRangeAsync(playerCharacter, opponent);
        await _context.SaveChangesAsync();

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
        var battle = await _battleService.CreateBattleVsOpponentAsync(playerCharacter.Id, opponent.Id);
        await _battleService.FinalizeAndApplyRewardsAsync(battle);

        // Assert - Losses should not award any XP or Fidelis
        battle.AttackerXP.Should().Be(0, "losses should not award XP");
        battle.AttackerFidelis.Should().Be(0m, "losses should not award Fidelis");

        // Verify character XP was not increased
        var updatedCharacter = await _characterRepository.GetByIdAsync(playerCharacter.Id);
        updatedCharacter!.XP.Should().Be(initialXP, "character should not gain XP from losing");

        // Verify user Fidelis was not changed
        var updatedUser = await _context.Users.FindAsync("user1");
        updatedUser!.FidelisBalance.Should().Be(initialFidelis, "losses should not award Fidelis");
    }

    [Fact]
    public async Task CreateBattleVsOpponentAsync_WithDraw_ShouldApplyDrawRewards()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "user1", UserName = "testuser", FidelisBalance = 100m,
            NormalizedUserName = "TESTUSER", Email = "t@t.com", NormalizedEmail = "T@T.COM",
            SecurityStamp = Guid.NewGuid().ToString(), ConcurrencyStamp = Guid.NewGuid().ToString(),
            FirstName = "Test", LastName = "User", Nickname = "tester", PhoneNumber = "000000000"
        };
        var playerCharacter = Character.Create("user1");
        var opponent = Character.Create("user2");

        _context.Users.Add(user);
        await _context.Characters.AddRangeAsync(playerCharacter, opponent);
        await _context.SaveChangesAsync();

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
        var battle = await _battleService.CreateBattleVsOpponentAsync(playerCharacter.Id, opponent.Id);
        await _battleService.FinalizeAndApplyRewardsAsync(battle);

        // Assert
        // Level 1 vs Level 1: levelDiff = 0, levelDiffMult = 1.0
        // XP = round(50 * 1.0) = 50
        battle.AttackerXP.Should().Be(50);
        // Fidelis = round(40 * 1.0, 2) = 40.00
        battle.AttackerFidelis.Should().Be(40.00m);

        // Verify character XP was updated
        var updatedCharacter = await _characterRepository.GetByIdAsync(playerCharacter.Id);
        updatedCharacter!.XP.Should().BeGreaterThan(initialXP);

        // Verify user Fidelis was updated via DbContext
        var updatedUser = await _context.Users.FindAsync("user1");
        updatedUser!.FidelisBalance.Should().Be(initialFidelis + 40.00m);
    }

    [Fact]
    public async Task CreateBattleVsOpponentAsync_ShouldNotApplyRewardsToDefender()
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
            AttackerFinalHP = 50,
            DefenderFinalHP = 0
        };

        _combatEngineMock.Setup(e => e.Simulate(It.IsAny<Character>(), It.IsAny<Character>(), It.IsAny<int>()))
            .Returns(combatResult);

        // Act
        var battle = await _battleService.CreateBattleVsOpponentAsync(playerCharacter.Id, opponent.Id);

        // Assert - battle completed successfully
        battle.Should().NotBeNull();
        battle.AttackerCharacterId.Should().Be(playerCharacter.Id);
        battle.DefenderCharacterId.Should().Be(opponent.Id);

        // Verify opponent character XP was not updated
        var opponentCharacter = await _characterRepository.GetByIdAsync(opponent.Id);
        opponentCharacter!.XP.Should().Be(0); // Should remain unchanged
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
        await _battleService.FinalizeAndApplyRewardsAsync(battle);

        // Assert
        // Level 1 vs higher level: levelDiff > 0 → levelDiffMult > 1.0
        // XP = round(BaseWinXP(100) * levelDiffMult) > 100
        battle.AttackerXP.Should().BeGreaterThan(100, "because higher-level opponents should give bonus XP");

        // Verify XP was applied to character (may have leveled up, so check level or XP increased)
        var updatedCharacter = await _characterRepository.GetByIdAsync(playerCharacter.Id);
        (updatedCharacter!.XP + (updatedCharacter.Level - 1) * 100).Should().BeGreaterThan(initialPlayerXP,
            "character should have gained XP (may have leveled up)");
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
        // Level 6+ vs Level 1: levelDiff < 0 → levelDiffMult < 1.0
        // XP = round(BaseWinXP(100) * levelDiffMult) < 100
        battle.AttackerXP.Should().BeLessThan(100, "because lower-level opponents should give less XP");
        battle.AttackerXP.Should().BeGreaterThan(0, "but should still give some XP since within reasonable level range");
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
        // Level 1 vs Level 1: levelDiff = 0, levelDiffMult = 1.0
        // XP = round(BaseWinXP(100) * 1.0) = 100
        battle.AttackerXP.Should().Be(100, "because fighting an equal level opponent should give base XP");
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private IDbContextFactory<ApplicationDbContext> CreateContextFactory()
    {
        var mock = new Mock<IDbContextFactory<ApplicationDbContext>>();
        mock.Setup(f => f.CreateDbContext()).Returns(() =>
            new ApplicationDbContext(_dbOptions, Mock.Of<IHttpContextAccessor>(), new AuditContext(), new AuditLogAppender()));
        return mock.Object;
    }
}
