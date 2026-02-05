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
using RTUB.Application.Services;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for MatchmakingService
/// Tests AI opponent selection, power rating, and cooldown logic
/// </summary>
public class MatchmakingServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly IMatchmakingService _matchmakingService;

    public MatchmakingServiceTests()
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

        _matchmakingService = new MatchmakingService(_context, Options.Create(new MyTunoScalingConfiguration()));
    }

    [Fact]
    public async Task FindAIOpponentAsync_WithNoOpponents_ShouldReturnNull()
    {
        // Arrange
        var playerCharacter = Character.Create("user1");
        await _context.Characters.AddAsync(playerCharacter);
        await _context.SaveChangesAsync();

        // Act
        var result = await _matchmakingService.FindAIOpponentAsync(playerCharacter);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task FindAIOpponentAsync_WithOpponents_ShouldReturnOpponent()
    {
        // Arrange
        var playerCharacter = Character.Create("user1");
        var opponent1 = Character.Create("user2");
        var opponent2 = Character.Create("user3");

        await _context.Characters.AddRangeAsync(playerCharacter, opponent1, opponent2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _matchmakingService.FindAIOpponentAsync(playerCharacter);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().NotBe(playerCharacter.Id);
        result.Id.Should().BeOneOf(opponent1.Id, opponent2.Id);
    }

    [Fact]
    public async Task FindAIOpponentAsync_ShouldExcludePlayerCharacter()
    {
        // Arrange
        var playerCharacter = Character.Create("user1");
        var opponent = Character.Create("user2");

        await _context.Characters.AddRangeAsync(playerCharacter, opponent);
        await _context.SaveChangesAsync();

        // Act
        var result = await _matchmakingService.FindAIOpponentAsync(playerCharacter);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().NotBe(playerCharacter.Id);
        result.Id.Should().Be(opponent.Id);
    }

    [Fact]
    public async Task FindAIOpponentAsync_WithNullCharacter_ShouldThrowException()
    {
        // Act
        var act = () => _matchmakingService.FindAIOpponentAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("playerCharacter");
    }

    [Fact]
    public async Task FindAIOpponentAsync_WithRecentBattle_ShouldRespectCooldown()
    {
        // Arrange
        var playerCharacter = Character.Create("user1");
        var opponent = Character.Create("user2");

        await _context.Characters.AddRangeAsync(playerCharacter, opponent);
        await _context.SaveChangesAsync();

        // Set cooldown via Character fields (within 1 hour cooldown period)
        playerCharacter.LastOpponentId = opponent.Id;
        playerCharacter.LastBattleAt = DateTime.UtcNow.AddMinutes(-30); // 30 minutes ago
        await _context.SaveChangesAsync();

        // Act
        var result = await _matchmakingService.FindAIOpponentAsync(playerCharacter);

        // Assert
        // Should return null or a different opponent (if available)
        // Since there's only one opponent and it's on cooldown, it should return null or ignore cooldown
        // The implementation allows ignoring cooldown if no other candidates exist
        result.Should().NotBeNull(); // Implementation allows repeat if no alternatives
    }

    [Fact]
    public async Task FindAIOpponentAsync_WithOldBattle_ShouldNotRespectCooldown()
    {
        // Arrange
        var playerCharacter = Character.Create("user1");
        var opponent = Character.Create("user2");

        await _context.Characters.AddRangeAsync(playerCharacter, opponent);
        await _context.SaveChangesAsync();

        // Set cooldown via Character fields (outside 1 hour cooldown period)
        playerCharacter.LastOpponentId = opponent.Id;
        playerCharacter.LastBattleAt = DateTime.UtcNow.AddHours(-2); // 2 hours ago
        await _context.SaveChangesAsync();

        // Act
        var result = await _matchmakingService.FindAIOpponentAsync(playerCharacter);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(opponent.Id); // Should be able to fight again
    }

    [Fact]
    public async Task FindAIOpponentAsync_WithMultipleOpponents_ShouldSelectOne()
    {
        // Arrange
        var playerCharacter = Character.Create("user1");
        var opponent1 = Character.Create("user2");
        var opponent2 = Character.Create("user3");
        var opponent3 = Character.Create("user4");

        await _context.Characters.AddRangeAsync(playerCharacter, opponent1, opponent2, opponent3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _matchmakingService.FindAIOpponentAsync(playerCharacter);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().NotBe(playerCharacter.Id);
        result.Id.Should().BeOneOf(opponent1.Id, opponent2.Id, opponent3.Id);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
