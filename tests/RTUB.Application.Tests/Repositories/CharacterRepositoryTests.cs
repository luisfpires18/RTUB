using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Repositories;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Repositories;

/// <summary>
/// Unit tests for CharacterRepository
/// Tests opponent selection and prioritization
/// </summary>
public class CharacterRepositoryTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly CharacterRepository _repository;

    public CharacterRepositoryTests(DatabaseFixture fixture)
    {
        // Clean database at constructor start to ensure test isolation
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();

        _context = _fixture.CreateContext();
        _repository = new CharacterRepository(_context);
    }

    [Fact]
    public async Task GetRandomOpponentsAsync_ReturnsBalancedOpponents()
    {
        // Arrange - Create player at level 5
        var playerUser = CreateTestUser("player-user", "Player", MemberCategory.Tuno);
        await _context.Users.AddAsync(playerUser);
        await _context.SaveChangesAsync();

        var playerCharacter = Character.Create(playerUser.Id);
        // Level up to 5
        for (int i = 1; i < 5; i++)
        {
            playerCharacter.AddXP(playerCharacter.Level * 100);
        }
        await _context.Characters.AddAsync(playerCharacter);
        await _context.SaveChangesAsync();

        // Create opponents at various levels
        var opponents = new List<Character>();
        for (int level = 1; level <= 10; level++)
        {
            if (level == 5) continue; // Skip player's level for this test

            var user = CreateTestUser($"user-{level}", $"Opponent{level}", MemberCategory.Tuno);
            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();

            var character = Character.Create(user.Id);
            // Level up to target level
            for (int i = 1; i < level; i++)
            {
                character.AddXP(character.Level * 100);
            }
            await _context.Characters.AddAsync(character);
            opponents.Add(character);
        }
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetRandomOpponentsAsync(playerCharacter.Id, 8);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(8);

        // Should have balanced distribution: 4 below/equal, 4 above
        var belowOrEqualCount = result.Count(c => c.Level <= playerCharacter.Level);
        var aboveCount = result.Count(c => c.Level > playerCharacter.Level);

        // With 4 below (1,2,3,4) and 5 above (6,7,8,9,10), we should get 4 of each
        belowOrEqualCount.Should().Be(4);
        aboveCount.Should().Be(4);
    }

    [Fact]
    public async Task GetRandomOpponentsAsync_SortsAboveLevelByProximity()
    {
        // Arrange - Create player at level 5
        var playerUser = CreateTestUser("player-user", "Player", MemberCategory.Tuno);
        await _context.Users.AddAsync(playerUser);
        await _context.SaveChangesAsync();

        var playerCharacter = Character.Create(playerUser.Id);
        // Level up to 5
        for (int i = 1; i < 5; i++)
        {
            playerCharacter.AddXP(playerCharacter.Level * 100);
        }
        await _context.Characters.AddAsync(playerCharacter);
        await _context.SaveChangesAsync();

        // Create higher level opponents only
        var levels = new[] { 10, 6, 8, 7 }; // Player is level 5
        foreach (var level in levels)
        {
            var user = CreateTestUser($"user-{level}", $"Opponent{level}", MemberCategory.Tuno);
            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();

            var character = Character.Create(user.Id);
            // Level up to target level
            for (int i = 1; i < level; i++)
            {
                character.AddXP(character.Level * 100);
            }
            await _context.Characters.AddAsync(character);
        }
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetRandomOpponentsAsync(playerCharacter.Id, 8);

        // Assert - Should be ordered by proximity: 6, 7, 8, 10
        result.Should().NotBeNull();
        result.Should().HaveCount(4);
        // All are above player level, sorted by proximity (ascending)
        result[0].Level.Should().Be(6); // Closest to 5
        result[1].Level.Should().Be(7);
        result[2].Level.Should().Be(8);
        result[3].Level.Should().Be(10); // Furthest from 5
    }

    [Fact]
    public async Task GetRandomOpponentsAsync_SortsBelowLevelByProximity()
    {
        // Arrange - Create player at level 10
        var playerUser = CreateTestUser("player-user", "Player", MemberCategory.Tuno);
        await _context.Users.AddAsync(playerUser);
        await _context.SaveChangesAsync();

        var playerCharacter = Character.Create(playerUser.Id);
        // Level up to 10
        for (int i = 1; i < 10; i++)
        {
            playerCharacter.AddXP(playerCharacter.Level * 100);
        }
        await _context.Characters.AddAsync(playerCharacter);
        await _context.SaveChangesAsync();

        // Create lower level opponents only
        var levels = new[] { 1, 9, 5, 7 }; // Player is level 10
        foreach (var level in levels)
        {
            var user = CreateTestUser($"user-{level}", $"Opponent{level}", MemberCategory.Tuno);
            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();

            var character = Character.Create(user.Id);
            // Level up to target level
            for (int i = 1; i < level; i++)
            {
                character.AddXP(character.Level * 100);
            }
            await _context.Characters.AddAsync(character);
        }
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetRandomOpponentsAsync(playerCharacter.Id, 8);

        // Assert - Below/equal opponents should be sorted by level descending (closest first)
        // All are below player level, sorted by proximity (descending by level)
        result.Should().NotBeNull();
        result.Should().HaveCount(4);
        result[0].Level.Should().Be(9); // Closest to 10
        result[1].Level.Should().Be(7);
        result[2].Level.Should().Be(5);
        result[3].Level.Should().Be(1); // Furthest from 10
    }

    [Fact]
    public async Task GetRandomOpponentsAsync_ReturnsUpTo8Opponents()
    {
        // Arrange - Create player
        var playerUser = CreateTestUser("player-user", "Player", MemberCategory.Tuno);
        await _context.Users.AddAsync(playerUser);
        await _context.SaveChangesAsync();

        var playerCharacter = Character.Create(playerUser.Id);
        await _context.Characters.AddAsync(playerCharacter);
        await _context.SaveChangesAsync();

        // Create 15 opponents
        for (int i = 0; i < 15; i++)
        {
            var user = CreateTestUser($"user-{i}", $"Opponent{i}", MemberCategory.Tuno);
            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();

            var character = Character.Create(user.Id);
            await _context.Characters.AddAsync(character);
        }
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetRandomOpponentsAsync(playerCharacter.Id, 8);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(8); // Should return exactly 8 when more are available
    }

    private ApplicationUser CreateTestUser(string id, string username, MemberCategory category = MemberCategory.Tuno)
    {
        return new ApplicationUser
        {
            Id = id,
            UserName = username,
            Email = $"{username}@test.com",
            Nickname = username,
            FirstName = username,
            LastName = "Test",
            Categories = new List<MemberCategory> { category },
            FidelisBalance = 100m
        };
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
