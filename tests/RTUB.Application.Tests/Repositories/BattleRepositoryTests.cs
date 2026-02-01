using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Repositories;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Repositories;

/// <summary>
/// Unit tests for BattleRepository
/// Tests leaderboard queries and battle data retrieval
/// </summary>
public class BattleRepositoryTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly BattleRepository _repository;

    public BattleRepositoryTests(DatabaseFixture fixture)
    {
        // Clean database at constructor start to ensure test isolation
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();

        _context = _fixture.CreateContext();
        _repository = new BattleRepository(_context);
    }

    [Fact]
    public async Task GetTopLeaderboardAsync_WithNoBattles_ReturnsEmptyList()
    {
        // Arrange - no battles in database

        // Act
        var result = await _repository.GetTopLeaderboardAsync(10);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTopLeaderboardAsync_WithBattles_ReturnsCorrectLeaderboard()
    {
        // Arrange
        var user1 = CreateTestUser("test-user1", "Player1");
        var user2 = CreateTestUser("test-user2", "Player2");
        var user3 = CreateTestUser("test-user3", "Player3");
        
        _context.Users.AddRange(user1, user2, user3);
        await _context.SaveChangesAsync();

        var char1 = Character.Create(user1.Id);
        var char2 = Character.Create(user2.Id);
        var char3 = Character.Create(user3.Id);
        
        _context.Characters.AddRange(char1, char2, char3);
        await _context.SaveChangesAsync();

        // Create battles with different outcomes
        // char1 wins 3 times
        _context.Battles.Add(CreateBattle(char1.Id, char2.Id, BattleOutcome.AttackerWon));
        _context.Battles.Add(CreateBattle(char1.Id, char3.Id, BattleOutcome.AttackerWon));
        _context.Battles.Add(CreateBattle(char2.Id, char1.Id, BattleOutcome.DefenderWon)); // char1 wins as defender
        
        // char2 wins 1 time
        _context.Battles.Add(CreateBattle(char2.Id, char3.Id, BattleOutcome.AttackerWon));
        
        // char3 has no wins (loses all battles)
        
        // Add a draw (shouldn't count for wins)
        _context.Battles.Add(CreateBattle(char1.Id, char2.Id, BattleOutcome.Draw));
        
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetTopLeaderboardAsync(10);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        
        // Verify ordering by wins
        result[0].Wins.Should().Be(3); // char1
        result[0].DisplayName.Should().Be("Player1");
        result[1].Wins.Should().Be(1); // char2
        result[1].DisplayName.Should().Be("Player2");
        result[2].Wins.Should().Be(0); // char3
        result[2].DisplayName.Should().Be("Player3");
    }

    [Fact]
    public async Task GetTopLeaderboardAsync_RespectsCountLimit()
    {
        // Arrange
        for (int i = 0; i < 5; i++)
        {
            var user = CreateTestUser($"test-user-limit-{i}", $"Player{i}");
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            
            var character = Character.Create(user.Id);
            _context.Characters.Add(character);
            await _context.SaveChangesAsync();
        }

        // Act
        var result = await _repository.GetTopLeaderboardAsync(3);

        // Assert
        result.Should().HaveCount(3);
    }

    private ApplicationUser CreateTestUser(string id, string nickname)
    {
        return new ApplicationUser
        {
            Id = id,
            UserName = $"{nickname}@test.com",
            Email = $"{nickname}@test.com",
            FirstName = "Test",
            LastName = nickname,
            Nickname = nickname,
            EmailConfirmed = true
        };
    }

    private Battle CreateBattle(int attackerId, int defenderId, BattleOutcome outcome)
    {
        var battle = Battle.Create(attackerId, defenderId, Random.Shared.Next(), outcome);
        battle.SetRewards(50, 10m);
        battle.SetReplay("{}");
        return battle;
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
