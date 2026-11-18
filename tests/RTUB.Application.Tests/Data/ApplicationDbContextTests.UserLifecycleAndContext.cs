using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Data;

// ApplicationDbContextTests: User Lifecycle And Context tests.
public partial class ApplicationDbContextTests
{
    [Fact]
    public async Task SaveChangesAsync_WhenUserProfileAndCriticalFieldChanged_MarksAsCritical()
    {
        // Arrange - Create a user
        var user = new ApplicationUser
        {
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            PhoneNumber = "123456789",
            Nickname = "TestUser",
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Clear previous audit logs for cleaner test
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act - Modify both profile info AND a critical field
        var userFromDb = await _context.Users.FindAsync(user.Id);
        userFromDb.Should().NotBeNull();
        userFromDb!.FirstName = "Updated"; // Profile field (non-critical)
        userFromDb.PhoneNumber = "987654321"; // Critical field
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();

        var auditLog = auditLogs.First();
        auditLog.EntityType.Should().Be("ApplicationUser");
        auditLog.Action.Should().Be("Modified");
        auditLog.IsCriticalAction.Should().BeTrue(); // Should be critical due to PhoneNumber change
        auditLog.Changes.Should().Contain("FirstName");
        auditLog.Changes.Should().Contain("PhoneNumber");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenUserLastLoginDateChanged_DoesNotCreateAuditLog()
    {
        // Arrange - Create a user
        var user = new ApplicationUser
        {
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            PhoneNumber = "123456789",
            Nickname = "TestUser",
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Clear previous audit logs for cleaner test
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act - Modify only LastLoginDate
        var userFromDb = await _context.Users.FindAsync(user.Id);
        userFromDb.Should().NotBeNull();
        userFromDb!.LastLoginDate = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Assert - LastLoginDate is excluded from audit logs as it's tracked separately in application logs
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().BeEmpty("LastLoginDate changes should not create audit logs");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenUserDeleted_MarksAsCritical()
    {
        // Arrange - Create a user
        var user = new ApplicationUser
        {
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            PhoneNumber = "123456789",
            Nickname = "TestUser",
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        var userId = user.Id;

        // Clear previous audit logs for cleaner test
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act - Delete the user
        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();

        var auditLog = auditLogs.First();
        auditLog.EntityType.Should().Be("ApplicationUser");
        auditLog.Action.Should().Be("Deleted");
        auditLog.IsCriticalAction.Should().BeTrue(); // User deletions are always critical
        auditLog.Changes.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SaveChangesAsync_WhenUserModified_IncludesTargetUserIdentification()
    {
        // Arrange - Create a user
        var user = new ApplicationUser
        {
            UserName = "targetuser",
            Email = "target@example.com",
            FirstName = "Target",
            LastName = "User",
            PhoneNumber = "123456789",
            Nickname = "TestUser",
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Clear previous audit logs for cleaner test
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act - Modify the user
        var userFromDb = await _context.Users.FindAsync(user.Id);
        userFromDb.Should().NotBeNull();
        userFromDb!.FirstName = "UpdatedTarget";
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();

        var auditLog = auditLogs.First();
        auditLog.EntityType.Should().Be("ApplicationUser");
        auditLog.Changes.Should().NotBeNullOrEmpty();

        // Verify the changes contain _TargetUser identification in simplified string format
        auditLog.Changes.Should().Contain("_TargetUser");
        auditLog.Changes.Should().Contain("targetuser");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenHttpContextUnavailable_UsesAuditContext()
    {
        // Arrange
        // Set HttpContext to null to simulate Blazor InteractiveServer scenario
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns((HttpContext)null!);

        // Set AuditContext with user information
        var auditUsername = "blazoruser";
        var auditUserId = "blazor123";
        _auditContext.SetUser(auditUsername, auditUserId);

        var album = Album.Create("Test Album", 2024);

        // Act
        _context.Albums.Add(album);
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();

        var auditLog = auditLogs.First();
        auditLog.UserName.Should().Be(auditUsername);
        auditLog.UserId.Should().Be(auditUserId);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenBothHttpContextAndAuditContextAvailable_PrefersHttpContext()
    {
        // Arrange
        // Setup HttpContext with one user
        var httpUsername = "httpuser";
        SetupMockUser(httpUsername);

        // Setup AuditContext with different user
        _auditContext.SetUser("audituser", "audit123");

        var album = Album.Create("Test Album", 2024);

        // Act
        _context.Albums.Add(album);
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();

        var auditLog = auditLogs.First();
        // HttpContext should take precedence over AuditContext
        auditLog.UserName.Should().Be(httpUsername);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenNeitherHttpContextNorAuditContextAvailable_UsesNullUser()
    {
        // Arrange
        // Set HttpContext to null
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns((HttpContext)null!);

        // Clear AuditContext
        _auditContext.Clear();

        var album = Album.Create("Test Album", 2024);

        // Act
        _context.Albums.Add(album);
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();

        var auditLog = auditLogs.First();
        auditLog.UserName.Should().BeNull();
        auditLog.UserId.Should().BeNull();
    }
}
