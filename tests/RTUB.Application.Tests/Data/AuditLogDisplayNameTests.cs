using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Data;

/// <summary>
/// Tests to verify that audit logs have proper EntityDisplayName values
/// for EventRepertoire, Enrollment, and RehearsalAttendance entities
/// </summary>
public class AuditLogDisplayNameTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
    private readonly AuditContext _auditContext;

    public AuditLogDisplayNameTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        _auditContext = new AuditContext();

        // Set up HttpContext with an authenticated user
        var httpContextMock = new Mock<HttpContext>();
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContextMock.Object);

        _context = new ApplicationDbContext(options, _httpContextAccessorMock.Object, _auditContext);
    }

    [Fact]
    public async Task EventRepertoire_Created_HasEventAndSongDisplayName()
    {
        // Arrange
        _auditContext.SetUser("testuser", "test-user-id");

        var evt = Event.Create("Concert 2024", DateTime.UtcNow.AddDays(30), "Test Location", EventType.Atuacao);
        _context.Events.Add(evt);
        await _context.SaveChangesAsync();

        var song = Song.Create("Amazing Song", 1);
        _context.Songs.Add(song);
        await _context.SaveChangesAsync();

        // Clear previous audit logs
        _context.AuditLogs.RemoveRange(_context.AuditLogs);
        await _context.SaveChangesAsync();

        // Act - Create EventRepertoire with navigation properties loaded
        var repertoire = EventRepertoire.Create(evt.Id, song.Id, 1, DateTime.UtcNow);
        repertoire.Event = evt;
        repertoire.Song = song;
        _context.EventRepertoires.Add(repertoire);
        await _context.SaveChangesAsync();

        // Assert
        var auditLog = await _context.AuditLogs
            .Where(a => a.EntityType == "EventRepertoire" && a.Action == "Created")
            .FirstOrDefaultAsync();

        auditLog.Should().NotBeNull();
        auditLog!.EntityDisplayName.Should().Be("Concert 2024 - Amazing Song");
    }

    [Fact]
    public async Task EventRepertoire_Deleted_HasEventAndSongDisplayName()
    {
        // Arrange
        _auditContext.SetUser("testuser", "test-user-id");

        var evt = Event.Create("Festival 2024", DateTime.UtcNow.AddDays(60), "Test Venue", EventType.Festival);
        _context.Events.Add(evt);
        await _context.SaveChangesAsync();

        var song = Song.Create("Beautiful Song", 1);
        _context.Songs.Add(song);
        await _context.SaveChangesAsync();

        var repertoire = EventRepertoire.Create(evt.Id, song.Id, 1, DateTime.UtcNow);
        repertoire.Event = evt;
        repertoire.Song = song;
        _context.EventRepertoires.Add(repertoire);
        await _context.SaveChangesAsync();

        // Clear previous audit logs
        _context.AuditLogs.RemoveRange(_context.AuditLogs);
        await _context.SaveChangesAsync();

        // Act - Delete EventRepertoire with navigation properties loaded
        _context.EventRepertoires.Remove(repertoire);
        await _context.SaveChangesAsync();

        // Assert
        var auditLog = await _context.AuditLogs
            .Where(a => a.EntityType == "EventRepertoire" && a.Action == "Deleted")
            .FirstOrDefaultAsync();

        auditLog.Should().NotBeNull();
        auditLog!.EntityDisplayName.Should().Be("Festival 2024 - Beautiful Song");
    }

    [Fact]
    public async Task Enrollment_Created_HasEventDisplayName()
    {
        // Arrange
        _auditContext.SetUser("testuser", "test-user-id");

        var user = new ApplicationUser
        {
            UserName = "johndoe",
            Email = "john@example.com",
            FirstName = "John",
            LastName = "Doe",
            Nickname = "JDoe"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var evt = Event.Create("Workshop 2024", DateTime.UtcNow.AddDays(15), "Conference Center", EventType.Convivio);
        _context.Events.Add(evt);
        await _context.SaveChangesAsync();

        // Clear previous audit logs
        _context.AuditLogs.RemoveRange(_context.AuditLogs);
        await _context.SaveChangesAsync();

        // Act - Create Enrollment with Event and User navigation properties loaded
        var enrollment = Enrollment.Create(user.Id, evt.Id);
        enrollment.Event = evt;
        enrollment.User = user;
        _context.Enrollments.Add(enrollment);
        await _context.SaveChangesAsync();

        // Assert
        var auditLog = await _context.AuditLogs
            .Where(a => a.EntityType == "Enrollment" && a.Action == "Created")
            .FirstOrDefaultAsync();

        auditLog.Should().NotBeNull();
        auditLog!.EntityDisplayName.Should().Be("JDoe - Workshop 2024 - Vai", "should show user nickname, event name, and attendance status");
        auditLog.TargetMemberName.Should().Be("JDoe", "should use the user's nickname");
    }

    [Fact]
    public async Task RehearsalAttendance_Created_HasUserDisplayName()
    {
        // Arrange
        _auditContext.SetUser("testuser", "test-user-id");

        var user = new ApplicationUser
        {
            UserName = "janedoe",
            Email = "jane@example.com",
            FirstName = "Jane",
            LastName = "Doe",
            Nickname = "JaneDoe"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var rehearsal = Rehearsal.Create(DateTime.UtcNow.AddDays(7), "Room A");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        // Clear previous audit logs
        _context.AuditLogs.RemoveRange(_context.AuditLogs);
        await _context.SaveChangesAsync();

        // Act - Create RehearsalAttendance with navigation properties loaded
        var attendance = RehearsalAttendance.Create(rehearsal.Id, user.Id, InstrumentType.Fagote);
        attendance.User = user;
        attendance.Rehearsal = rehearsal;
        _context.RehearsalAttendances.Add(attendance);
        await _context.SaveChangesAsync();

        // Assert
        var auditLog = await _context.AuditLogs
            .Where(a => a.EntityType == "RehearsalAttendance" && a.Action == "Created")
            .FirstOrDefaultAsync();

        auditLog.Should().NotBeNull();
        auditLog!.EntityDisplayName.Should().StartWith("JaneDoe - ").And.EndWith(" - Vai");
        auditLog.TargetMemberName.Should().Be("JaneDoe", "should use the user's nickname");
    }

    [Fact]
    public async Task RehearsalAttendance_Deleted_HasUserDisplayNameNotId()
    {
        // Arrange
        _auditContext.SetUser("testuser", "test-user-id");

        var user = new ApplicationUser
        {
            UserName = "bobsmith",
            Email = "bob@example.com",
            FirstName = "Bob",
            LastName = "Smith",
            Nickname = "BobS"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var rehearsal = Rehearsal.Create(DateTime.UtcNow.AddDays(14), "Room B");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var attendance = RehearsalAttendance.Create(rehearsal.Id, user.Id, InstrumentType.Contrabaixo);
        attendance.User = user;
        attendance.Rehearsal = rehearsal;
        _context.RehearsalAttendances.Add(attendance);
        await _context.SaveChangesAsync();

        // Clear previous audit logs
        _context.AuditLogs.RemoveRange(_context.AuditLogs);
        await _context.SaveChangesAsync();

        // Act - Delete RehearsalAttendance with navigation properties loaded
        _context.RehearsalAttendances.Remove(attendance);
        await _context.SaveChangesAsync();

        // Assert
        var auditLog = await _context.AuditLogs
            .Where(a => a.EntityType == "RehearsalAttendance" && a.Action == "Deleted")
            .FirstOrDefaultAsync();

        auditLog.Should().NotBeNull();
        auditLog!.EntityDisplayName.Should().StartWith("BobS - ").And.EndWith(" - Vai");
        auditLog.EntityDisplayName.Should().NotContain(user.Id, "Display name should not contain user ID");
        auditLog.TargetMemberName.Should().Be("BobS", "should use the user's nickname");
    }

    [Fact]
    public async Task EventRepertoire_WithoutNavigationProperties_FallsBackToLocalCache()
    {
        // Arrange
        _auditContext.SetUser("testuser", "test-user-id");

        var evt = Event.Create("Gala 2024", DateTime.UtcNow.AddDays(45), "Grand Hall", EventType.Arraial);
        _context.Events.Add(evt);
        await _context.SaveChangesAsync();

        var song = Song.Create("Wonderful Song", 1);
        _context.Songs.Add(song);
        await _context.SaveChangesAsync();

        // Clear previous audit logs
        _context.AuditLogs.RemoveRange(_context.AuditLogs);
        await _context.SaveChangesAsync();

        // Act - Create EventRepertoire WITHOUT loading navigation properties
        // The entities should still be in Local cache from SaveChangesAsync above
        var repertoire = EventRepertoire.Create(evt.Id, song.Id, 1, DateTime.UtcNow);
        // DON'T set navigation properties: repertoire.Event = evt; repertoire.Song = song;
        _context.EventRepertoires.Add(repertoire);
        await _context.SaveChangesAsync();

        // Assert
        var auditLog = await _context.AuditLogs
            .Where(a => a.EntityType == "EventRepertoire" && a.Action == "Created")
            .FirstOrDefaultAsync();

        auditLog.Should().NotBeNull();
        // Should still get display name from Local cache as fallback
        auditLog!.EntityDisplayName.Should().Be("Gala 2024 - Wonderful Song");
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
