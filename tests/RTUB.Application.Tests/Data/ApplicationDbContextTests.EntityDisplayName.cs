using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Data;

// ApplicationDbContextTests: Entity Display Name tests.
public partial class ApplicationDbContextTests
{
    [Fact]
    public async Task GetEntityDisplayName_ResolvesNamesTheTrackerLacks_FromTheBatchLoadedBeforeTheSave()
    {
        // #220 (was: only the Local cache, so the name was null). Arrange - Create entities but don't load them into context
        var song = new Song { Title = "Test Song", AlbumId = 1 };
        _context.Songs.Add(song);
        await _context.SaveChangesAsync();

        // Clear the change tracker so Song is not in Local cache
        _context.ChangeTracker.Clear();

        // Create a SongYouTubeUrl that references the song
        var youtubeUrl = new SongYouTubeUrl { SongId = song.Id, Url = "https://youtube.com/test" };
        _context.SongYouTubeUrls.Add(youtubeUrl);

        // Act - Save changes which triggers GetEntityDisplayName
        // Song is not in the Local cache: its title comes from the one batched lookup made before the save
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.Where(a => a.EntityType == "SongYouTubeUrl").ToListAsync();
        auditLogs.Should().ContainSingle();

        var auditLog = auditLogs.First();
        auditLog.EntityDisplayName.Should().Be("Test Song · YouTube");
    }

    [Fact]
    public async Task GetEntityDisplayName_UsesLocalCache_WhenEntitiesInCache()
    {
        // Arrange - Create entities and keep them in context
        var song = new Song { Title = "Test Song", AlbumId = 1 };
        _context.Songs.Add(song);
        await _context.SaveChangesAsync();

        // Clear existing audit logs
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Create a SongYouTubeUrl - Song is now in Local cache
        var youtubeUrl = new SongYouTubeUrl { SongId = song.Id, Url = "https://youtube.com/test" };
        _context.SongYouTubeUrls.Add(youtubeUrl);

        // Act - Save changes which triggers GetEntityDisplayName
        await _context.SaveChangesAsync();

        // Assert - Audit log should have resolved entity name from Local cache
        var auditLogs = await _context.AuditLogs.Where(a => a.EntityType == "SongYouTubeUrl").ToListAsync();
        auditLogs.Should().ContainSingle();

        var auditLog = auditLogs.First();
        auditLog.EntityDisplayName.Should().Be("Test Song · YouTube");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenRoleAddedAndEntitiesNotInLocalCache_ResolvesNamesAsynchronously()
    {
        // Arrange - Create role and user in database first
        var testRole = new Microsoft.AspNetCore.Identity.IdentityRole
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Coordinator",
            NormalizedName = "COORDINATOR"
        };
        _context.Roles.Add(testRole);

        var testUser = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "coordinator1",
            Email = "coordinator@example.com",
            FirstName = "Coord",
            LastName = "Inator",
            PhoneNumber = "123456789",
            Nickname = "CoordUser",
        };
        _context.Users.Add(testUser);
        await _context.SaveChangesAsync();

        // Clear audit logs
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Clear the change tracker to ensure user and role are NOT in Local cache
        _context.ChangeTracker.Clear();

        // Verify they are not in Local cache
        _context.Users.Local.Should().BeEmpty();
        _context.Roles.Local.Should().BeEmpty();

        // Act - Add user to role (user and role not in Local cache, must be resolved asynchronously)
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
        auditLog.IsCriticalAction.Should().BeTrue();

        // Verify the changes contain resolved username and role name (not IDs)
        // This proves async resolution worked correctly
        auditLog.Changes.Should().Contain("coordinator1");
        auditLog.Changes.Should().Contain("Coordinator");
        auditLog.Changes.Should().NotContain(testUser.Id);
        auditLog.Changes.Should().NotContain(testRole.Id);
        auditLog.EntityDisplayName.Should().Be("coordinator1 - Coordinator");
    }
}
