using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Data;

// ApplicationDbContextTests: Collection And Role Changes tests.
public partial class ApplicationDbContextTests
{
    [Fact]
    public async Task SaveChangesAsync_WhenCollectionPropertyUnchanged_DoesNotLogChange()
    {
        // Arrange - Create a user with Categories and Positions
        var user = new ApplicationUser
        {
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            PhoneNumber = "123456789",
            Nickname = "TestUser",
            Categories = new List<MemberCategory> { MemberCategory.Tuno },
            Positions = new List<Position> { Position.Ensaiador }
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Clear previous audit logs for cleaner test
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act - Load the user from DB, modify one property, then save
        // This simulates a real scenario where EF detects actual changes
        var userFromDb = await _context.Users.FindAsync(user.Id);
        userFromDb.Should().NotBeNull(); // Explicit null check for clarity
        userFromDb!.PhoneNumber = "987654321";
        // Collections are tracked by reference - setting the same values should not cause change
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();

        var auditLog = auditLogs.First();
        auditLog.Changes.Should().NotBeNullOrEmpty();

        // The audit log should only contain PhoneNumber change, not Categories or Positions
        auditLog.Changes.Should().Contain("PhoneNumber");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenEmptyCollectionsUnchanged_DoesNotLogChange()
    {
        // Arrange - Create a user with empty collections
        var user = new ApplicationUser
        {
            UserName = "testuser2",
            Email = "test2@example.com",
            FirstName = "Test",
            LastName = "User",
            PhoneNumber = "123456789",
            Nickname = "TestUser",
            Categories = new List<MemberCategory>(),  // Empty collection
            Positions = new List<Position>()    // Empty collection
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Clear previous audit logs for cleaner test
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act - Load the user from DB, modify one property, then save
        var userFromDb = await _context.Users.FindAsync(user.Id);
        userFromDb.Should().NotBeNull();
        userFromDb!.PhoneNumber = "987654321";
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();

        var auditLog = auditLogs.First();
        auditLog.Changes.Should().NotBeNullOrEmpty();

        // The audit log should only contain PhoneNumber change
        auditLog.Changes.Should().Contain("PhoneNumber");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenCollectionsChanged_LogsChange()
    {
        // Arrange - Create a user with empty collections
        var user = new ApplicationUser
        {
            UserName = "testuser3",
            Email = "test3@example.com",
            FirstName = "Test",
            LastName = "User",
            PhoneNumber = "123456789",
            Nickname = "TestUser",
            Categories = new List<MemberCategory>(),  // Empty initially
            Positions = new List<Position>()    // Empty initially
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Clear previous audit logs for cleaner test
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act - Load the user from DB, add values to collections
        var userFromDb = await _context.Users.FindAsync(user.Id);
        userFromDb.Should().NotBeNull();
        userFromDb!.Categories.Add(MemberCategory.Tuno);
        userFromDb.Positions.Add(Position.Magister);
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();

        var auditLog = auditLogs.First();
        auditLog.Changes.Should().NotBeNullOrEmpty();

        // The audit log should contain the collection changes
        auditLog.Changes.Should().Contain("Categories");
        auditLog.Changes.Should().Contain("Positions");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenRoleAdded_CreatesAuditLogWithUsernameAndRoleName()
    {
        // Arrange
        // Create a test role
        var testRole = new Microsoft.AspNetCore.Identity.IdentityRole
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Member",
            NormalizedName = "MEMBER"
        };
        _context.Roles.Add(testRole);

        // Create a test user
        var testUser = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "jeans",
            Email = "jeans@example.com",
            FirstName = "Jean",
            LastName = "Smith",
            PhoneNumber = "123456789",
            Nickname = "TestUser",
        };
        _context.Users.Add(testUser);
        await _context.SaveChangesAsync();

        // Clear previous audit logs for cleaner test
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act - Add user to role
        var userRole = new Microsoft.AspNetCore.Identity.IdentityUserRole<string>
        {
            UserId = testUser.Id,
            RoleId = testRole.Id
        };
        _context.UserRoles.Add(userRole);
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();

        var auditLog = auditLogs.First();
        auditLog.EntityType.Should().Be("UserRole");
        auditLog.Action.Should().Be("Role Added");
        auditLog.UserName.Should().Be(_testUsername);
        auditLog.IsCriticalAction.Should().BeTrue();
        auditLog.Changes.Should().NotBeNullOrEmpty();

        // Verify the changes contain username and role name, not IDs
        auditLog.Changes.Should().Contain("jeans");
        auditLog.Changes.Should().Contain("Member");
        auditLog.Changes.Should().NotContain(testUser.Id);
        auditLog.Changes.Should().NotContain(testRole.Id);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenRoleRemoved_CreatesAuditLogWithUsernameAndRoleName()
    {
        // Arrange
        // Create a test role
        var testRole = new Microsoft.AspNetCore.Identity.IdentityRole
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Admin",
            NormalizedName = "ADMIN"
        };
        _context.Roles.Add(testRole);

        // Create a test user
        var testUser = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "jeans",
            Email = "jeans@example.com",
            FirstName = "Jean",
            LastName = "Smith",
            PhoneNumber = "123456789",
            Nickname = "TestUser",
        };
        _context.Users.Add(testUser);

        // Add user to role
        var userRole = new Microsoft.AspNetCore.Identity.IdentityUserRole<string>
        {
            UserId = testUser.Id,
            RoleId = testRole.Id
        };
        _context.UserRoles.Add(userRole);
        await _context.SaveChangesAsync();

        // Clear previous audit logs for cleaner test
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act - Remove user from role
        _context.UserRoles.Remove(userRole);
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();

        var auditLog = auditLogs.First();
        auditLog.EntityType.Should().Be("UserRole");
        auditLog.Action.Should().Be("Role Removed");
        auditLog.UserName.Should().Be(_testUsername);
        auditLog.IsCriticalAction.Should().BeTrue();
        auditLog.Changes.Should().NotBeNullOrEmpty();

        // Verify the changes contain username and role name, not IDs
        auditLog.Changes.Should().Contain("jeans");
        auditLog.Changes.Should().Contain("Admin");
        auditLog.Changes.Should().NotContain(testUser.Id);
        auditLog.Changes.Should().NotContain(testRole.Id);
    }
}
