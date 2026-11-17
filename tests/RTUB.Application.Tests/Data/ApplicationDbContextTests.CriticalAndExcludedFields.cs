using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Data;

// ApplicationDbContextTests: Critical And Excluded Fields tests.
public partial class ApplicationDbContextTests
{
    [Fact]
    public async Task SaveChangesAsync_WhenMultipleCriticalFieldsChanged_IncludesAllInCriticalFieldsModified()
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
            PasswordHash = "oldhash",
            SecurityStamp = "oldstamp"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Clear previous audit logs for cleaner test
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act - Modify multiple critical fields at once
        var userFromDb = await _context.Users.FindAsync(user.Id);
        userFromDb.Should().NotBeNull();
        userFromDb!.PasswordHash = "newhash";
        userFromDb.SecurityStamp = "newstamp";
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();

        var auditLog = auditLogs.First();
        auditLog.EntityType.Should().Be("ApplicationUser");
        auditLog.Action.Should().Be("Modified");
        auditLog.IsCriticalAction.Should().BeTrue();
        // Both critical fields should be listed in _CriticalFieldsModified
        auditLog.Changes.Should().Contain("_CriticalFieldsModified");
        auditLog.Changes.Should().Contain("PasswordHash");
        auditLog.Changes.Should().Contain("SecurityStamp");
        // But the actual values should NOT be logged
        auditLog.Changes.Should().NotContain("oldhash");
        auditLog.Changes.Should().NotContain("newhash");
        auditLog.Changes.Should().NotContain("oldstamp");
        auditLog.Changes.Should().NotContain("newstamp");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenOnlyExcludedFieldsChanged_DoesNotCreateAuditLog()
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
            SecurityStamp = "oldstamp"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Clear previous audit logs for cleaner test
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act - Modify only excluded fields that are NOT critical (EmailConfirmed, PhoneNumberConfirmed)
        var userFromDb = await _context.Users.FindAsync(user.Id);
        userFromDb.Should().NotBeNull();
        userFromDb!.EmailConfirmed = true;
        userFromDb.PhoneNumberConfirmed = true;
        await _context.SaveChangesAsync();

        // Assert - No audit log should be created because only non-critical excluded fields changed
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveChangesAsync_WhenExcludedAndNonExcludedFieldsChanged_CreatesAuditLogWithNonExcludedFields()
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
            SecurityStamp = "oldstamp"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Clear previous audit logs for cleaner test
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act - Modify both excluded and non-excluded fields
        var userFromDb = await _context.Users.FindAsync(user.Id);
        userFromDb.Should().NotBeNull();
        userFromDb!.SecurityStamp = "newstamp"; // Excluded
        userFromDb.FirstName = "Updated"; // Non-excluded
        await _context.SaveChangesAsync();

        // Assert - Audit log should be created with non-excluded field
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();

        var auditLog = auditLogs.First();
        auditLog.Changes.Should().Contain("FirstName");
        // SecurityStamp is excluded from logging but appears in _CriticalFieldsModified array
        auditLog.Changes.Should().Contain("_CriticalFieldsModified");
        auditLog.Changes.Should().Contain("SecurityStamp");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenMultipleExcludedFieldsAndOneNonExcluded_CreatesAuditLog()
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
            SecurityStamp = "oldstamp",
            PasswordHash = "oldhash"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Clear previous audit logs for cleaner test
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act - Modify multiple excluded fields AND one non-excluded field
        var userFromDb = await _context.Users.FindAsync(user.Id);
        userFromDb.Should().NotBeNull();
        userFromDb!.SecurityStamp = "newstamp"; // Excluded
        userFromDb.PasswordHash = "newhash"; // Excluded
        userFromDb.EmailConfirmed = true; // Excluded
        userFromDb.Nickname = "UpdatedNick"; // Non-excluded
        await _context.SaveChangesAsync();

        // Assert - Audit log should be created with only the non-excluded field
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();

        var auditLog = auditLogs.First();
        auditLog.Changes.Should().Contain("Nickname");
        // Critical fields appear in _CriticalFieldsModified array but their values are excluded
        auditLog.Changes.Should().Contain("_CriticalFieldsModified");
        auditLog.Changes.Should().Contain("SecurityStamp");
        auditLog.Changes.Should().Contain("PasswordHash");
        // Non-critical excluded fields like EmailConfirmed don't appear at all
        auditLog.Changes.Should().NotContain("EmailConfirmed");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenOnlyLastLoginDateChanged_DoesNotCreateAuditLog()
    {
        // Arrange - Create a user
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            PhoneNumber = "123456789",
            Nickname = "TestUser",
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Clear existing audit logs
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act - Update only LastLoginDate
        var userFromDb = await _context.Users.FirstAsync(u => u.Id == user.Id);
        userFromDb.LastLoginDate = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Assert - LastLoginDate is excluded from audit logs as it's tracked separately
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().BeEmpty("LastLoginDate changes should not create audit logs");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenLastLoginDateUpdatedViaAuditContext_DoesNotCreateAuditLog()
    {
        // Arrange - Create a user (simulating login scenario)
        var user = new ApplicationUser
        {
            UserName = "jeans",
            Email = "jeans@example.com",
            FirstName = "Jean",
            LastName = "Smith",
            PhoneNumber = "123456789",
            Nickname = "TestUser",
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Clear previous audit logs for cleaner test
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Clear mock HttpContext to simulate login scenario where user hasn't signed in yet
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        // Act - Update LastLoginDate using AuditContext (simulating login process)
        var userFromDb = await _context.Users.FindAsync(user.Id);
        userFromDb.Should().NotBeNull();

        // Set AuditContext to track the user who is logging in
        _auditContext.SetUser(userFromDb!.UserName, userFromDb.Id);

        userFromDb.LastLoginDate = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Clear AuditContext after save
        _auditContext.Clear();

        // Assert - LastLoginDate is excluded from audit logs
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().BeEmpty("LastLoginDate changes should not create audit logs");
    }
}
