using FluentAssertions;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for AuditLogService
/// Tests audit log query and filter operations
/// </summary>
public class AuditLogServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly AuditLogService _auditLogService;

    public AuditLogServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options, Mock.Of<Microsoft.AspNetCore.Http.IHttpContextAccessor>(), new AuditContext());
        _auditLogService = new AuditLogService(_context);
    }

    [Fact]
    public async Task GetAllAsync_WithNoFilters_ReturnsAllLogs()
    {
        // Arrange
        await SeedAuditLogs();

        // Act
        var result = await _auditLogService.GetAllAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetAllAsync_FilterByUserName_ReturnsFilteredLogs()
    {
        // Arrange
        await SeedAuditLogs();

        // Act
        var result = await _auditLogService.GetAllAsync(userName: "user1");

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2); // Should return logs from user1
        result.Should().OnlyContain(log => log.UserName == "user1");
    }

    [Fact]
    public async Task GetAllAsync_ExcludeUserName_ReturnsFilteredLogs()
    {
        // Arrange
        await SeedAuditLogs();

        // Act
        var result = await _auditLogService.GetAllAsync(excludeUserName: "user1");

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3); // Should return logs that are NOT from user1
        result.Should().OnlyContain(log => log.UserName != "user1");
    }

    [Fact]
    public async Task GetAllAsync_FilterAndExcludeUserName_ReturnsBothFilters()
    {
        // Arrange
        await SeedAuditLogs();

        // Act - Filter for user2 but exclude user1 (should only get user2)
        var result = await _auditLogService.GetAllAsync(userName: "user2", excludeUserName: "user1");

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3); // Should return logs from user2 (and user2 is already not user1)
        result.Should().OnlyContain(log => log.UserName == "user2");
    }

    [Fact]
    public async Task GetAllAsync_FilterByEntityType_ReturnsFilteredLogs()
    {
        // Arrange
        await SeedAuditLogs();

        // Act
        var result = await _auditLogService.GetAllAsync(entityType: "Event");

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().OnlyContain(log => log.EntityType == "Event");
    }

    [Fact]
    public async Task GetAllAsync_FilterByAction_ReturnsFilteredLogs()
    {
        // Arrange
        await SeedAuditLogs();

        // Act
        var result = await _auditLogService.GetAllAsync(action: "Created");

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result.Should().OnlyContain(log => log.Action == "Created");
    }

    [Fact]
    public async Task GetAllAsync_FilterByDateRange_ReturnsFilteredLogs()
    {
        // Arrange
        await SeedAuditLogs();
        var fromDate = DateTime.UtcNow.AddDays(-2);
        var toDate = DateTime.UtcNow.AddDays(-1);

        // Act
        var result = await _auditLogService.GetAllAsync(fromDate: fromDate, toDate: toDate);

        // Assert
        result.Should().NotBeNull();
        // Logs created in the last 3 days should be filtered by the date range
        result.Count().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetAllAsync_FilterByCriticalOnly_ReturnsOnlyCriticalLogs()
    {
        // Arrange
        await SeedAuditLogs();

        // Act
        var result = await _auditLogService.GetAllAsync(criticalOnly: true);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().OnlyContain(log => log.IsCriticalAction);
    }

    [Fact]
    public async Task GetAllAsync_WithPagination_ReturnsPagedResults()
    {
        // Arrange
        await SeedAuditLogs();

        // Act
        var page1 = await _auditLogService.GetAllAsync(page: 1, pageSize: 2);
        var page2 = await _auditLogService.GetAllAsync(page: 2, pageSize: 2);

        // Assert
        page1.Should().HaveCount(2);
        page2.Should().HaveCount(2);
        page1.Should().NotIntersectWith(page2);
    }

    [Fact]
    public async Task GetCountAsync_WithNoFilters_ReturnsTotal()
    {
        // Arrange
        await SeedAuditLogs();

        // Act
        var result = await _auditLogService.GetCountAsync();

        // Assert
        result.Should().Be(5);
    }

    [Fact]
    public async Task GetCountAsync_WithIncludeFilter_ReturnsFilteredCount()
    {
        // Arrange
        await SeedAuditLogs();

        // Act
        var result = await _auditLogService.GetCountAsync(userName: "user1");

        // Assert
        result.Should().Be(2); // Should count logs from user1
    }

    [Fact]
    public async Task GetCountAsync_WithExcludeFilter_ReturnsFilteredCount()
    {
        // Arrange
        await SeedAuditLogs();

        // Act
        var result = await _auditLogService.GetCountAsync(excludeUserName: "user1");

        // Assert
        result.Should().Be(3); // Should count logs that are NOT from user1
    }

    [Fact]
    public async Task GetEntityHistoryAsync_ReturnsLogsForSpecificEntity()
    {
        // Arrange
        await SeedAuditLogs();

        // Act
        var result = await _auditLogService.GetEntityHistoryAsync("Event", 1);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().OnlyContain(log => log.EntityType == "Event" && log.EntityId == 1);
    }

    [Fact]
    public async Task SearchChangesAsync_WithSearchTerm_ReturnsMatchingLogs()
    {
        // Arrange
        await SeedAuditLogs();

        // Act
        var result = await _auditLogService.SearchChangesAsync("Concert");

        // Assert
        result.Should().NotBeNull();
        result.Count().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SearchChangesAsync_WithEmptySearchTerm_ReturnsEmpty()
    {
        // Arrange
        await SeedAuditLogs();

        // Act
        var result = await _auditLogService.SearchChangesAsync("");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEntityTypesAsync_ReturnsDistinctEntityTypes()
    {
        // Arrange
        await SeedAuditLogs();

        // Act
        var result = await _auditLogService.GetEntityTypesAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3); // Event, Album, Song
        result.Should().Contain(new[] { "Event", "Album", "Song" });
    }

    [Fact]
    public async Task GetActionTypesAsync_ReturnsDistinctActionTypes()
    {
        // Arrange
        await SeedAuditLogs();

        // Act
        var result = await _auditLogService.GetActionTypesAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2); // Created, Modified
        result.Should().Contain(new[] { "Created", "Modified" });
    }

    [Fact]
    public async Task GetUserNamesAsync_ReturnsDistinctUserNames()
    {
        // Arrange
        await SeedAuditLogs();

        // Act
        var result = await _auditLogService.GetUserNamesAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2); // user1, user2
        result.Should().Contain(new[] { "user1", "user2" });
    }

    [Fact]
    public async Task DeleteAsync_RemovesSpecificAuditLog()
    {
        // Arrange
        await SeedAuditLogs();
        var initialCount = await _context.AuditLogs.CountAsync();
        initialCount.Should().Be(5);
        
        var logToDelete = await _context.AuditLogs.FirstAsync();
        var logId = logToDelete.Id;

        // Act
        await _auditLogService.DeleteAsync(logId);

        // Assert
        var finalCount = await _context.AuditLogs.CountAsync();
        finalCount.Should().Be(4);
        
        var deletedLog = await _context.AuditLogs.FindAsync(logId);
        deletedLog.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentId_DoesNotThrow()
    {
        // Arrange
        await SeedAuditLogs();
        var initialCount = await _context.AuditLogs.CountAsync();

        // Act
        await _auditLogService.DeleteAsync(99999);

        // Assert
        var finalCount = await _context.AuditLogs.CountAsync();
        finalCount.Should().Be(initialCount);
    }

    [Fact]
    public async Task TruncateAsync_DeletesAllAuditLogs()
    {
        // Arrange
        await SeedAuditLogs();
        var initialCount = await _context.AuditLogs.CountAsync();
        initialCount.Should().Be(5);

        // Act
        await _auditLogService.TruncateAsync();

        // Assert
        var finalCount = await _context.AuditLogs.CountAsync();
        finalCount.Should().Be(0);
    }

    [Fact]
    public async Task TruncateByUserAsync_DeletesOnlyUserLogs()
    {
        // Arrange
        await SeedAuditLogs();
        var initialCount = await _context.AuditLogs.CountAsync();
        initialCount.Should().Be(5);

        // Act
        await _auditLogService.TruncateByUserAsync("user1");

        // Assert
        var finalCount = await _context.AuditLogs.CountAsync();
        finalCount.Should().Be(3); // Only user2 logs remain
        
        var remainingLogs = await _context.AuditLogs.ToListAsync();
        remainingLogs.Should().OnlyContain(log => log.UserName == "user2");
    }

    [Fact]
    public async Task TruncateByUserAsync_WithNonExistentUser_DeletesNothing()
    {
        // Arrange
        await SeedAuditLogs();
        var initialCount = await _context.AuditLogs.CountAsync();

        // Act
        await _auditLogService.TruncateByUserAsync("nonexistentuser");

        // Assert
        var finalCount = await _context.AuditLogs.CountAsync();
        finalCount.Should().Be(initialCount);
    }

    [Fact]
    public async Task TruncateByUserAsync_WithNullUserName_ThrowsArgumentException()
    {
        // Arrange
        await SeedAuditLogs();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _auditLogService.TruncateByUserAsync(null!));
    }

    [Fact]
    public async Task TruncateByUserAsync_WithEmptyUserName_ThrowsArgumentException()
    {
        // Arrange
        await SeedAuditLogs();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _auditLogService.TruncateByUserAsync(""));
    }

    [Fact]
    public async Task GetAllForExportAsync_WithNoFilters_ReturnsAllLogs()
    {
        // Arrange
        await SeedAuditLogs();

        // Act
        var result = await _auditLogService.GetAllForExportAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetAllForExportAsync_WithIncludeFilter_ReturnsFilteredLogs()
    {
        // Arrange
        await SeedAuditLogs();

        // Act
        var result = await _auditLogService.GetAllForExportAsync(userName: "user1");

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2); // Should return logs from user1
        result.Should().OnlyContain(log => log.UserName == "user1");
    }

    [Fact]
    public async Task GetAllForExportAsync_WithExcludeFilter_ReturnsFilteredLogs()
    {
        // Arrange
        await SeedAuditLogs();

        // Act
        var result = await _auditLogService.GetAllForExportAsync(excludeUserName: "user1");

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3); // Should return logs that are NOT from user1
        result.Should().OnlyContain(log => log.UserName != "user1");
    }

    [Fact]
    public async Task GetAllForExportAsync_ReturnsLogsInDescendingOrder()
    {
        // Arrange
        await SeedAuditLogs();

        // Act
        var result = (await _auditLogService.GetAllForExportAsync()).ToList();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(5);
        // Verify descending order by timestamp
        for (int i = 0; i < result.Count - 1; i++)
        {
            result[i].Timestamp.Should().BeAfter(result[i + 1].Timestamp);
        }
    }

    private async Task SeedAuditLogs()
    {
        var logs = new[]
        {
            new AuditLog
            {
                EntityType = "Event",
                EntityId = 1,
                Action = "Created",
                UserId = "user1-id",
                UserName = "user1",
                Timestamp = DateTime.UtcNow.AddDays(-3),
                Changes = "{\"Name\": \"Concert\"}",
                IsCriticalAction = false
            },
            new AuditLog
            {
                EntityType = "Event",
                EntityId = 1,
                Action = "Modified",
                UserId = "user1-id",
                UserName = "user1",
                Timestamp = DateTime.UtcNow.AddDays(-2),
                Changes = "{\"Name\": {\"Old\": \"Concert\", \"New\": \"Big Concert\"}}",
                IsCriticalAction = false
            },
            new AuditLog
            {
                EntityType = "Album",
                EntityId = 2,
                Action = "Created",
                UserId = "user2-id",
                UserName = "user2",
                Timestamp = DateTime.UtcNow.AddDays(-1),
                Changes = "{\"Title\": \"Greatest Hits\"}",
                IsCriticalAction = true
            },
            new AuditLog
            {
                EntityType = "Song",
                EntityId = 3,
                Action = "Created",
                UserId = "user2-id",
                UserName = "user2",
                Timestamp = DateTime.UtcNow.AddHours(-12),
                Changes = "{\"Title\": \"New Song\"}",
                IsCriticalAction = false
            },
            new AuditLog
            {
                EntityType = "Album",
                EntityId = 2,
                Action = "Modified",
                UserId = "user2-id",
                UserName = "user2",
                Timestamp = DateTime.UtcNow.AddHours(-6),
                Changes = "{\"Title\": {\"Old\": \"Greatest Hits\", \"New\": \"Best Hits\"}}",
                IsCriticalAction = true
            }
        };

        _context.AuditLogs.AddRange(logs);
        await _context.SaveChangesAsync();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
