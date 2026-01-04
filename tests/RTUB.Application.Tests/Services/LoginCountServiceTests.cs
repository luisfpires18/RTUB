using FluentAssertions;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Tests.Fixtures;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Application.Repositories;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for LoginCountService
/// </summary>
public class LoginCountServiceTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly LoginCountService _service;
    private readonly LoginCountRepository _repository;

    public LoginCountServiceTests(DatabaseFixture fixture)
    {
        // Clean database at constructor start to ensure test isolation
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();

        _context = _fixture.CreateContext();
        _repository = new LoginCountRepository(_context);
        _service = new LoginCountService(_repository);
    }

    [Fact]
    public async Task GetLoginCountForDateAsync_WithExistingRecord_ReturnsCount()
    {
        // Arrange
        var userId = "user123";
        var date = DateTime.UtcNow.Date;
        var loginCount = new LoginCount
        {
            UserId = userId,
            LoginDate = date,
            Count = 5
        };
        _context.LoginCounts.Add(loginCount);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetLoginCountForDateAsync(userId, date);

        // Assert
        result.Should().Be(5);
    }

    [Fact]
    public async Task GetLoginCountForDateAsync_WithNonExistingRecord_ReturnsZero()
    {
        // Arrange
        var userId = "user123";
        var date = DateTime.UtcNow.Date;

        // Act
        var result = await _service.GetLoginCountForDateAsync(userId, date);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task GetLoginHistoryAsync_ReturnsAllLoginCountsOrderedByDateDescending()
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
        var result = await _service.GetLoginHistoryAsync(userId);

        // Assert
        result.Should().HaveCount(3);
        result[0].LoginDate.Should().Be(date3); // Most recent first
        result[1].LoginDate.Should().Be(date2);
        result[2].LoginDate.Should().Be(date1);
    }

    [Fact]
    public async Task GetLoginHistoryAsync_WithNoRecords_ReturnsEmptyList()
    {
        // Arrange
        var userId = "user123";

        // Act
        var result = await _service.GetLoginHistoryAsync(userId);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetLoginHistoryForDateRangeAsync_ReturnsOnlyRecordsInRange()
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

        // Act
        var result = await _service.GetLoginHistoryForDateRangeAsync(userId, date2, date3);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(lc => lc.LoginDate == date2);
        result.Should().Contain(lc => lc.LoginDate == date3);
    }

    [Fact]
    public async Task GetTotalLoginCountAsync_SumsAllCountsForUser()
    {
        // Arrange
        var userId = "user123";
        var date1 = new DateTime(2024, 1, 15).Date;
        var date2 = new DateTime(2024, 1, 16).Date;
        var date3 = new DateTime(2024, 1, 17).Date;

        _context.LoginCounts.AddRange(
            new LoginCount { UserId = userId, LoginDate = date1, Count = 5 },
            new LoginCount { UserId = userId, LoginDate = date2, Count = 3 },
            new LoginCount { UserId = userId, LoginDate = date3, Count = 2 }
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetTotalLoginCountAsync(userId);

        // Assert
        result.Should().Be(10); // 5 + 3 + 2
    }

    [Fact]
    public async Task GetTotalLoginCountAsync_WithNoRecords_ReturnsZero()
    {
        // Arrange
        var userId = "user123";

        // Act
        var result = await _service.GetTotalLoginCountAsync(userId);

        // Assert
        result.Should().Be(0);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
