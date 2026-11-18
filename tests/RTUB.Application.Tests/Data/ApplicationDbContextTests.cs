using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Data;

/// <summary>
/// Unit tests for ApplicationDbContext
/// Tests automatic CreatedBy and UpdatedBy field population
/// </summary>
public partial class ApplicationDbContextTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
    private readonly AuditContext _auditContext;
    private readonly string _testUsername = "testuser";

    public ApplicationDbContextTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        _auditContext = new AuditContext();
        SetupMockUser(_testUsername);

        _context = new ApplicationDbContext(options, _httpContextAccessorMock.Object, _auditContext, new RTUB.Application.Services.AuditLogAppender());
    }

    private void SetupMockUser(string username)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, username)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        var httpContextMock = new Mock<HttpContext>();
        httpContextMock.Setup(x => x.User).Returns(claimsPrincipal);

        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContextMock.Object);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenEntityCreated_SetsCreatedByField()
    {
        // Arrange
        var album = Album.Create("Test Album", 2024);

        // Act
        _context.Albums.Add(album);
        await _context.SaveChangesAsync();

        // Assert
        album.CreatedBy.Should().Be(_testUsername);
        album.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        album.UpdatedBy.Should().BeNull();
        album.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public async Task SaveChangesAsync_WhenEntityModified_SetsUpdatedByField()
    {
        // Arrange
        var album = Album.Create("Test Album", 2024);
        _context.Albums.Add(album);
        await _context.SaveChangesAsync();

        var originalCreatedBy = album.CreatedBy;
        var originalCreatedAt = album.CreatedAt;

        // Act
        album.UpdateDetails("Updated Album", 2025, "Updated description", false);
        _context.Albums.Update(album);
        await _context.SaveChangesAsync();

        // Assert
        album.CreatedBy.Should().Be(originalCreatedBy);
        album.CreatedAt.Should().Be(originalCreatedAt);
        album.UpdatedBy.Should().Be(_testUsername);
        album.UpdatedAt.Should().NotBeNull();
        album.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task SaveChangesAsync_WhenMultipleEntitiesCreated_SetsCreatedByFieldForAll()
    {
        // Arrange
        var album1 = Album.Create("Album 1", 2024);
        var album2 = Album.Create("Album 2", 2024);

        // Act
        _context.Albums.Add(album1);
        _context.Albums.Add(album2);
        await _context.SaveChangesAsync();

        // Assert
        album1.CreatedBy.Should().Be(_testUsername);
        album2.CreatedBy.Should().Be(_testUsername);
    }

    [Fact]
    public async Task SaveChangesAsync_WithDifferentUser_TracksCorrectUser()
    {
        // Arrange
        var album = Album.Create("Test Album", 2024);
        _context.Albums.Add(album);
        await _context.SaveChangesAsync();

        // Change user
        var newUsername = "newuser";
        SetupMockUser(newUsername);

        // Act
        album.UpdateDetails("Updated Album", 2025, null, false);
        _context.Albums.Update(album);
        await _context.SaveChangesAsync();

        // Assert
        album.CreatedBy.Should().Be(_testUsername);
        album.UpdatedBy.Should().Be(newUsername);
    }

    [Fact]
    public async Task SaveChangesAsync_WithNoUser_DoesNotSetCreatedBy()
    {
        // Arrange
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns((HttpContext)null!);
        var album = Album.Create("Test Album", 2024);

        // Act
        _context.Albums.Add(album);
        await _context.SaveChangesAsync();

        // Assert
        album.CreatedBy.Should().BeNull();
        album.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task SaveChangesAsync_ForVariousEntityTypes_SetsFieldsCorrectly()
    {
        // Arrange
        var album = Album.Create("Test Album", 2024);
        var song = Song.Create("Test Song", 1, 1);

        // Act
        _context.Albums.Add(album);
        _context.Songs.Add(song);
        await _context.SaveChangesAsync();

        // Assert
        album.CreatedBy.Should().Be(_testUsername);
        song.CreatedBy.Should().Be(_testUsername);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenEntityCreated_CreatesAuditLog()
    {
        // Arrange
        var album = Album.Create("Test Album", 2024);

        // Act
        _context.Albums.Add(album);
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();

        var auditLog = auditLogs.First();
        auditLog.EntityType.Should().Be("Album");
        auditLog.EntityId.Should().Be(album.Id);
        auditLog.Action.Should().Be("Created");
        auditLog.UserName.Should().Be(_testUsername);
        auditLog.Changes.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SaveChangesAsync_WhenEntityModified_CreatesAuditLog()
    {
        // Arrange
        var album = Album.Create("Test Album", 2024);
        _context.Albums.Add(album);
        await _context.SaveChangesAsync();

        // Clear previous audit logs for cleaner test
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act
        album.UpdateDetails("Updated Album", 2025, "Updated description", false);
        _context.Albums.Update(album);
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();

        var auditLog = auditLogs.First();
        auditLog.EntityType.Should().Be("Album");
        auditLog.EntityId.Should().Be(album.Id);
        auditLog.Action.Should().Be("Modified");
        auditLog.UserName.Should().Be(_testUsername);
        auditLog.Changes.Should().NotBeNullOrEmpty();
        auditLog.Changes.Should().Contain("Title");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenEntityDeleted_CreatesAuditLog()
    {
        // Arrange
        var album = Album.Create("Test Album", 2024);
        _context.Albums.Add(album);
        await _context.SaveChangesAsync();
        var albumId = album.Id;

        // Clear previous audit logs for cleaner test
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act
        _context.Albums.Remove(album);
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();

        var auditLog = auditLogs.First();
        auditLog.EntityType.Should().Be("Album");
        auditLog.EntityId.Should().Be(albumId);
        auditLog.Action.Should().Be("Deleted");
        auditLog.UserName.Should().Be(_testUsername);
        auditLog.IsCriticalAction.Should().BeTrue(); // Deletions are critical
    }

    [Fact]
    public async Task SaveChangesAsync_CriticalEntity_MarksAuditLogAsCritical()
    {
        // Arrange - Report is considered a critical entity
        var report = Report.Create("Financial Report 2024", 2024, "Summary");

        // Act
        _context.Reports.Add(report);
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.Where(a => a.EntityType == "Report").ToListAsync();
        auditLogs.Should().ContainSingle();
        auditLogs.First().IsCriticalAction.Should().BeTrue();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
