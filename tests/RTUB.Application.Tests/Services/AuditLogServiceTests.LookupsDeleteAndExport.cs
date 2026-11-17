using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Repositories;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

// AuditLogServiceTests: Lookups Delete And Export tests.
public partial class AuditLogServiceTests
{
    [Fact]
    public async Task HiddenEntityTypes_AreExcludedFromResults()
    {
        // Arrange
        await SeedAuditLogs();
        _context.AuditLogs.Add(new AuditLog
        {
            EntityType = "Message",
            EntityId = 99,
            Action = "Created",
            UserId = "user3-id",
            UserName = "user3",
            Timestamp = DateTime.UtcNow,
            Changes = "{}",
            IsCriticalAction = false
        });

        _context.AuditLogs.Add(new AuditLog
        {
            EntityType = "Conversation",
            EntityId = 99,
            Action = "Created",
            UserId = "user3-id",
            UserName = "user3",
            Timestamp = DateTime.UtcNow,
            Changes = "{}",
            IsCriticalAction = false
        });

        _context.AuditLogs.Add(new AuditLog
        {
            EntityType = "ConversationUserSettings",
            EntityId = 99,
            Action = "Created",
            UserId = "user3-id",
            UserName = "user3",
            Timestamp = DateTime.UtcNow,
            Changes = "{}",
            IsCriticalAction = false
        });

        await _context.SaveChangesAsync();

        // Act
        var (logs, totalCount) = await _auditLogService.GetPagedWithCountAsync(pageSize: 20);
        var entityTypes = await _auditLogService.GetEntityTypesAsync();

        // Assert
        logs.Should().NotContain(l => string.Equals(l.EntityType, "Message", StringComparison.OrdinalIgnoreCase));
        logs.Should().NotContain(l => string.Equals(l.EntityType, "Conversation", StringComparison.OrdinalIgnoreCase));
        logs.Should().NotContain(l => string.Equals(l.EntityType, "ConversationUserSettings", StringComparison.OrdinalIgnoreCase));
        totalCount.Should().Be(5); // Hidden entities are not counted
        entityTypes.Should().NotContain(type => string.Equals(type, "Message", StringComparison.OrdinalIgnoreCase));
        entityTypes.Should().NotContain(type => string.Equals(type, "Conversation", StringComparison.OrdinalIgnoreCase));
        entityTypes.Should().NotContain(type => string.Equals(type, "ConversationUserSettings", StringComparison.OrdinalIgnoreCase));
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
        _context.ChangeTracker.Clear();
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
}
