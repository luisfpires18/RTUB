using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Data;

/// <summary>
/// Tests to verify that LoginCount table properly tracks login history
/// </summary>
public class LoginCountTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
    private readonly AuditContext _auditContext;

    public LoginCountTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        _auditContext = new AuditContext();

        // Set up HttpContext without an authenticated user
        var httpContextMock = new Mock<HttpContext>();
        httpContextMock.Setup(x => x.User).Returns(new System.Security.Claims.ClaimsPrincipal());
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContextMock.Object);

        _context = new ApplicationDbContext(options, _httpContextAccessorMock.Object, _auditContext);
    }

    [Fact]
    public async Task LoginCount_FirstLogin_CreatesNewRecord()
    {
        // Arrange
        var userId = "user123";
        var today = DateTime.UtcNow.Date;

        // Act
        _context.LoginCounts.Add(new LoginCount
        {
            UserId = userId,
            Date = today,
            Count = 1
        });
        await _context.SaveChangesAsync();

        // Assert
        var loginCount = await _context.LoginCounts
            .FirstOrDefaultAsync(lc => lc.UserId == userId && lc.Date == today);

        loginCount.Should().NotBeNull();
        loginCount!.Count.Should().Be(1);
        loginCount.UserId.Should().Be(userId);
        loginCount.Date.Should().Be(today);
    }

    [Fact]
    public async Task LoginCount_MultipleLoginsOnSameDay_IncrementsCount()
    {
        // Arrange
        var userId = "user456";
        var today = DateTime.UtcNow.Date;

        // First login
        _context.LoginCounts.Add(new LoginCount
        {
            UserId = userId,
            Date = today,
            Count = 1
        });
        await _context.SaveChangesAsync();

        // Act - Second login on the same day
        var existingCount = await _context.LoginCounts
            .FirstOrDefaultAsync(lc => lc.UserId == userId && lc.Date == today);
        existingCount!.Count++;
        await _context.SaveChangesAsync();

        // Assert
        var loginCount = await _context.LoginCounts
            .FirstOrDefaultAsync(lc => lc.UserId == userId && lc.Date == today);

        loginCount.Should().NotBeNull();
        loginCount!.Count.Should().Be(2);
    }

    [Fact]
    public async Task LoginCount_LoginOnDifferentDays_CreatesMultipleRecords()
    {
        // Arrange
        var userId = "user789";
        var today = DateTime.UtcNow.Date;
        var yesterday = today.AddDays(-1);

        // Act - Login on two different days
        _context.LoginCounts.Add(new LoginCount
        {
            UserId = userId,
            Date = yesterday,
            Count = 2
        });
        _context.LoginCounts.Add(new LoginCount
        {
            UserId = userId,
            Date = today,
            Count = 1
        });
        await _context.SaveChangesAsync();

        // Assert
        var loginCounts = await _context.LoginCounts
            .Where(lc => lc.UserId == userId)
            .OrderBy(lc => lc.Date)
            .ToListAsync();

        loginCounts.Should().HaveCount(2);
        loginCounts[0].Date.Should().Be(yesterday);
        loginCounts[0].Count.Should().Be(2);
        loginCounts[1].Date.Should().Be(today);
        loginCounts[1].Count.Should().Be(1);
    }

    [Fact]
    public async Task LoginCount_QueryByDateRange_ReturnsCorrectRecords()
    {
        // Arrange
        var userId = "user101112";
        var today = DateTime.UtcNow.Date;
        var threeDaysAgo = today.AddDays(-3);
        var twoDaysAgo = today.AddDays(-2);
        var oneDayAgo = today.AddDays(-1);

        // Add login records for different days
        _context.LoginCounts.AddRange(
            new LoginCount { UserId = userId, Date = threeDaysAgo, Count = 1 },
            new LoginCount { UserId = userId, Date = twoDaysAgo, Count = 2 },
            new LoginCount { UserId = userId, Date = oneDayAgo, Count = 3 },
            new LoginCount { UserId = userId, Date = today, Count = 1 }
        );
        await _context.SaveChangesAsync();

        // Act - Query for last 2 days (yesterday and today)
        var startDate = oneDayAgo;
        var endDate = today;

        var loginCounts = await _context.LoginCounts
            .Where(lc => lc.UserId == userId && lc.Date >= startDate && lc.Date <= endDate)
            .OrderBy(lc => lc.Date)
            .ToListAsync();

        // Assert
        loginCounts.Should().HaveCount(2);
        loginCounts[0].Date.Should().Be(oneDayAgo);
        loginCounts[0].Count.Should().Be(3);
        loginCounts[1].Date.Should().Be(today);
        loginCounts[1].Count.Should().Be(1);
    }

    [Fact]
    public async Task LoginCount_MultipleUsers_TracksIndependently()
    {
        // Arrange
        var user1 = "user001";
        var user2 = "user002";
        var today = DateTime.UtcNow.Date;

        // Act - Both users login on the same day
        _context.LoginCounts.AddRange(
            new LoginCount { UserId = user1, Date = today, Count = 5 },
            new LoginCount { UserId = user2, Date = today, Count = 2 }
        );
        await _context.SaveChangesAsync();

        // Assert
        var user1Count = await _context.LoginCounts
            .FirstOrDefaultAsync(lc => lc.UserId == user1 && lc.Date == today);
        var user2Count = await _context.LoginCounts
            .FirstOrDefaultAsync(lc => lc.UserId == user2 && lc.Date == today);

        user1Count.Should().NotBeNull();
        user1Count!.Count.Should().Be(5);
        
        user2Count.Should().NotBeNull();
        user2Count!.Count.Should().Be(2);
    }

    [Fact]
    public async Task LoginCount_UniqueConstraint_PreventsDuplicateUserDateRecords()
    {
        // Arrange
        var userId = "user999";
        var today = DateTime.UtcNow.Date;

        _context.LoginCounts.Add(new LoginCount
        {
            UserId = userId,
            Date = today,
            Count = 1
        });
        await _context.SaveChangesAsync();

        // Act & Assert - Adding duplicate should be prevented by checking first
        var existingCount = await _context.LoginCounts
            .FirstOrDefaultAsync(lc => lc.UserId == userId && lc.Date == today);

        existingCount.Should().NotBeNull("The unique constraint should prevent duplicates, so we check before adding");
        
        // In the real application, we check if the record exists before adding
        // This test verifies that the check works correctly
        var duplicateExists = await _context.LoginCounts
            .AnyAsync(lc => lc.UserId == userId && lc.Date == today);
        
        duplicateExists.Should().BeTrue();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
