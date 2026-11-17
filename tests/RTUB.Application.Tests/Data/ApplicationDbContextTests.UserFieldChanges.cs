using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Data;

// ApplicationDbContextTests: User Field Changes tests.
public partial class ApplicationDbContextTests
{
    [Fact]
    public async Task SaveChangesAsync_WhenUserProfileInfoChanged_DoesNotMarkAsCritical()
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
            DateOfBirth = DateTime.Parse("1990-01-01"),
            Degree = "Computer Science"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Clear previous audit logs for cleaner test
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act - Modify only profile information
        var userFromDb = await _context.Users.FindAsync(user.Id);
        userFromDb.Should().NotBeNull();
        userFromDb!.FirstName = "Updated";
        userFromDb.LastName = "Name";
        userFromDb.DateOfBirth = DateTime.Parse("1991-02-02");
        userFromDb.Degree = "Software Engineering";
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();

        var auditLog = auditLogs.First();
        auditLog.EntityType.Should().Be("ApplicationUser");
        auditLog.Action.Should().Be("Modified");
        auditLog.IsCriticalAction.Should().BeFalse(); // Profile changes are NOT critical
        auditLog.Changes.Should().Contain("FirstName");
        auditLog.Changes.Should().Contain("LastName");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenUserEmailChanged_MarksAsCritical()
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

        // Act - Modify Email (critical field)
        var userFromDb = await _context.Users.FindAsync(user.Id);
        userFromDb.Should().NotBeNull();
        userFromDb!.Email = "newemail@example.com";
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();

        var auditLog = auditLogs.First();
        auditLog.EntityType.Should().Be("ApplicationUser");
        auditLog.Action.Should().Be("Modified");
        auditLog.IsCriticalAction.Should().BeTrue(); // Email changes are critical
        auditLog.Changes.Should().Contain("Email");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenUserNameChanged_MarksAsCritical()
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

        // Act - Modify UserName (critical field)
        var userFromDb = await _context.Users.FindAsync(user.Id);
        userFromDb.Should().NotBeNull();
        userFromDb!.UserName = "newusername";
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();

        var auditLog = auditLogs.First();
        auditLog.EntityType.Should().Be("ApplicationUser");
        auditLog.Action.Should().Be("Modified");
        auditLog.IsCriticalAction.Should().BeTrue(); // UserName changes are critical
        auditLog.Changes.Should().Contain("UserName");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenUserPhoneNumberChanged_MarksAsCritical()
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

        // Act - Modify PhoneNumber (critical field)
        var userFromDb = await _context.Users.FindAsync(user.Id);
        userFromDb.Should().NotBeNull();
        userFromDb!.PhoneNumber = "987654321";
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();

        var auditLog = auditLogs.First();
        auditLog.EntityType.Should().Be("ApplicationUser");
        auditLog.Action.Should().Be("Modified");
        auditLog.IsCriticalAction.Should().BeTrue(); // PhoneNumber changes are critical
        auditLog.Changes.Should().Contain("PhoneNumber");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenUserPasswordHashChanged_MarksAsCriticalButDoesNotLogValue()
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
            PasswordHash = "oldhash"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Clear previous audit logs for cleaner test
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act - Modify PasswordHash (critical field but excluded from logging)
        var userFromDb = await _context.Users.FindAsync(user.Id);
        userFromDb.Should().NotBeNull();
        userFromDb!.PasswordHash = "newhash";
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();

        var auditLog = auditLogs.First();
        auditLog.EntityType.Should().Be("ApplicationUser");
        auditLog.Action.Should().Be("Modified");
        auditLog.IsCriticalAction.Should().BeTrue(); // PasswordHash changes are critical
        // But the actual hash value should NOT be in the changes (excluded field)
        auditLog.Changes.Should().NotContain("oldhash");
        auditLog.Changes.Should().NotContain("newhash");
        // However, we should indicate WHICH critical field was modified
        auditLog.Changes.Should().Contain("_CriticalFieldsModified");
        auditLog.Changes.Should().Contain("PasswordHash");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenUserSecurityStampChanged_MarksAsCriticalButDoesNotLogValue()
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

        // Act - Modify SecurityStamp (critical field but excluded from logging)
        var userFromDb = await _context.Users.FindAsync(user.Id);
        userFromDb.Should().NotBeNull();
        userFromDb!.SecurityStamp = "newstamp";
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();

        var auditLog = auditLogs.First();
        auditLog.EntityType.Should().Be("ApplicationUser");
        auditLog.Action.Should().Be("Modified");
        auditLog.IsCriticalAction.Should().BeTrue(); // SecurityStamp changes are critical
        // But the actual stamp value should NOT be in the changes (excluded field)
        auditLog.Changes.Should().NotContain("oldstamp");
        auditLog.Changes.Should().NotContain("newstamp");
        // However, we should indicate WHICH critical field was modified
        auditLog.Changes.Should().Contain("_CriticalFieldsModified");
        auditLog.Changes.Should().Contain("SecurityStamp");
    }
}
