using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Repositories;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Repositories;

/// <summary>
/// Unit tests for LoginCountRepository
/// </summary>
public class LoginCountRepositoryTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly LoginCountRepository _repository;

    public LoginCountRepositoryTests(DatabaseFixture fixture)
    {
        // Clean database at constructor start to ensure test isolation
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();

        _context = _fixture.CreateContext();
        _repository = new LoginCountRepository(_context);
    }

    [Fact]
    public async Task GetByUserAndDateAsync_WithExistingRecord_ReturnsLoginCount()
    {
        // Arrange
        var userId = "user123";
        var loginDate = DateTime.UtcNow.Date;
        var loginCount = new LoginCount
        {
            UserId = userId,
            LoginDate = loginDate,
            Count = 5
        };
        _context.LoginCounts.Add(loginCount);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByUserAndDateAsync(userId, loginDate);

        // Assert
        result.Should().NotBeNull();
        result!.UserId.Should().Be(userId);
        result.LoginDate.Should().Be(loginDate);
        result.Count.Should().Be(5);
    }

    [Fact]
    public async Task GetByUserAndDateAsync_WithNonExistingRecord_ReturnsNull()
    {
        // Arrange
        var userId = "user123";
        var loginDate = DateTime.UtcNow.Date;

        // Act
        var result = await _repository.GetByUserAndDateAsync(userId, loginDate);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByUserIdAsync_ReturnsAllLoginCountsOrderedByDateDescending()
    {
        // Arrange
        var userId = "user123";
        var date1 = new DateTime(2024, 1, 15).Date;
        var date2 = new DateTime(2024, 1, 16).Date;
        var date3 = new DateTime(2024, 1, 17).Date;

        _context.LoginCounts.AddRange(
            new LoginCount { UserId = userId, LoginDate = date2, Count = 3 },
            new LoginCount { UserId = userId, LoginDate = date1, Count = 2 },
            new LoginCount { UserId = userId, LoginDate = date3, Count = 1 }
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByUserIdAsync(userId);

        // Assert
        result.Should().HaveCount(3);
        result[0].LoginDate.Should().Be(date3); // Most recent first
        result[1].LoginDate.Should().Be(date2);
        result[2].LoginDate.Should().Be(date1);
    }

    [Fact]
    public async Task GetByUserIdAsync_WithDifferentUsers_ReturnsOnlyMatchingUser()
    {
        // Arrange
        var userId1 = "user123";
        var userId2 = "user456";
        var date = DateTime.UtcNow.Date;

        _context.LoginCounts.AddRange(
            new LoginCount { UserId = userId1, LoginDate = date, Count = 2 },
            new LoginCount { UserId = userId2, LoginDate = date, Count = 3 }
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByUserIdAsync(userId1);

        // Assert
        result.Should().HaveCount(1);
        result[0].UserId.Should().Be(userId1);
    }

    [Fact]
    public async Task GetByUserIdAndDateRangeAsync_ReturnsOnlyRecordsInRange()
    {
        // Arrange
        var userId = "user123";
        var date1 = new DateTime(2024, 1, 10).Date;
        var date2 = new DateTime(2024, 1, 15).Date;
        var date3 = new DateTime(2024, 1, 20).Date;
        var date4 = new DateTime(2024, 1, 25).Date;

        _context.LoginCounts.AddRange(
            new LoginCount { UserId = userId, LoginDate = date1, Count = 1 },
            new LoginCount { UserId = userId, LoginDate = date2, Count = 2 },
            new LoginCount { UserId = userId, LoginDate = date3, Count = 3 },
            new LoginCount { UserId = userId, LoginDate = date4, Count = 4 }
        );
        await _context.SaveChangesAsync();

        // Act - Query from date2 to date3 (inclusive)
        var result = await _repository.GetByUserIdAndDateRangeAsync(userId, date2, date3);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(lc => lc.LoginDate == date2);
        result.Should().Contain(lc => lc.LoginDate == date3);
        result.Should().NotContain(lc => lc.LoginDate == date1);
        result.Should().NotContain(lc => lc.LoginDate == date4);
    }

    [Fact]
    public async Task GetTotalLoginCountByUserIdAsync_CountsAllRecordsForUser()
    {
        // Arrange
        var userId = "user123";
        var date1 = new DateTime(2024, 1, 15).Date;
        var date2 = new DateTime(2024, 1, 16).Date;
        var date3 = new DateTime(2024, 1, 17).Date;

        _context.LoginCounts.AddRange(
            new LoginCount { UserId = userId, LoginDate = date1, Count = 1 },
            new LoginCount { UserId = userId, LoginDate = date2, Count = 1 },
            new LoginCount { UserId = userId, LoginDate = date3, Count = 1 }
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetTotalLoginCountByUserIdAsync(userId);

        // Assert
        result.Should().Be(3); // 3 days with logins
    }

    [Fact]
    public async Task GetTotalLoginCountByUserIdAsync_WithNoRecords_ReturnsZero()
    {
        // Arrange
        var userId = "user123";

        // Act
        var result = await _repository.GetTotalLoginCountByUserIdAsync(userId);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task GetTotalLoginCountByUserIdAsync_OnlyCountsForSpecificUser()
    {
        // Arrange
        var userId1 = "user123";
        var userId2 = "user456";
        var date = DateTime.UtcNow.Date;

        _context.LoginCounts.AddRange(
            new LoginCount { UserId = userId1, LoginDate = date, Count = 1 },
            new LoginCount { UserId = userId2, LoginDate = date, Count = 1 }
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetTotalLoginCountByUserIdAsync(userId1);

        // Assert
        result.Should().Be(1); // Only 1 day for user1
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
