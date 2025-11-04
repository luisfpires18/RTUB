using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for RehearsalAttendanceService
/// Tests business logic and service layer operations
/// </summary>
public class RehearsalAttendanceServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly RehearsalAttendanceService _attendanceService;

    public RehearsalAttendanceServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _attendanceService = new RehearsalAttendanceService(_context);
    }

    [Fact]
    public async Task MarkAttendanceAsync_NewAttendance_CreatesAttendance()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var userId = "user123";
        var instrument = InstrumentType.Guitarra;

        // Act
        var result = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, instrument);

        // Assert
        result.Should().NotBeNull();
        result.RehearsalId.Should().Be(rehearsal.Id);
        result.UserId.Should().Be(userId);
        result.Instrument.Should().Be(instrument);
        result.Attended.Should().BeFalse(); // Defaults to false (pending approval)
    }

    [Fact]
    public async Task MarkAttendanceAsync_ExistingAttendance_UpdatesAttendance()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var userId = "user123";
        var attendance = RehearsalAttendance.Create(rehearsal.Id, userId, InstrumentType.Guitarra);
        _context.RehearsalAttendances.Add(attendance);
        await _context.SaveChangesAsync();

        // Act
        var result = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, InstrumentType.Bandolim);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(attendance.Id);
        result.Instrument.Should().Be(InstrumentType.Bandolim);
        result.Attended.Should().BeFalse(); // Stays pending until admin approval
    }

    [Fact]
    public async Task GetAttendanceByIdAsync_ExistingAttendance_ReturnsAttendance()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var attendance = RehearsalAttendance.Create(rehearsal.Id, "user123");
        _context.RehearsalAttendances.Add(attendance);
        await _context.SaveChangesAsync();

        // Clear change tracker to ensure fresh load
        _context.ChangeTracker.Clear();

        // Act
        var result = await _attendanceService.GetAttendanceByIdAsync(attendance.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(attendance.Id);
        result.UserId.Should().Be("user123");
    }

    [Fact]
    public async Task GetAttendanceByIdAsync_NonExistingAttendance_ReturnsNull()
    {
        // Act
        var result = await _attendanceService.GetAttendanceByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAttendancesByRehearsalIdAsync_ReturnsAllAttendances()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var att1 = RehearsalAttendance.Create(rehearsal.Id, "user1");
        var att2 = RehearsalAttendance.Create(rehearsal.Id, "user2");
        var att3 = RehearsalAttendance.Create(rehearsal.Id, "user3");
        _context.RehearsalAttendances.AddRange(att1, att2, att3);
        await _context.SaveChangesAsync();

        // Clear change tracker to ensure fresh load
        _context.ChangeTracker.Clear();

        // Act
        var result = await _attendanceService.GetAttendancesByRehearsalIdAsync(rehearsal.Id);

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetAttendancesByUserIdAsync_ReturnsUserAttendances()
    {
        // Arrange
        var rehearsal1 = Rehearsal.Create(DateTime.Now.AddDays(7), "Location 1");
        var rehearsal2 = Rehearsal.Create(DateTime.Now.AddDays(14), "Location 2");
        _context.Rehearsals.AddRange(rehearsal1, rehearsal2);
        await _context.SaveChangesAsync();

        var userId = "user123";
        _context.RehearsalAttendances.Add(RehearsalAttendance.Create(rehearsal1.Id, userId));
        _context.RehearsalAttendances.Add(RehearsalAttendance.Create(rehearsal2.Id, userId));
        _context.RehearsalAttendances.Add(RehearsalAttendance.Create(rehearsal1.Id, "otherUser"));
        await _context.SaveChangesAsync();

        // Act
        var result = await _attendanceService.GetAttendancesByUserIdAsync(userId);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(a => a.UserId == userId);
    }

    [Fact]
    public async Task UpdateAttendanceAsync_ValidId_UpdatesAttendance()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var attendance = RehearsalAttendance.Create(rehearsal.Id, "user123", InstrumentType.Guitarra);
        _context.RehearsalAttendances.Add(attendance);
        await _context.SaveChangesAsync();

        // Act
        await _attendanceService.UpdateAttendanceAsync(attendance.Id, false, InstrumentType.Bandolim);

        // Assert
        var updated = await _context.RehearsalAttendances.FindAsync(attendance.Id);
        updated!.Attended.Should().BeFalse();
        updated.Instrument.Should().Be(InstrumentType.Bandolim);
    }

    [Fact]
    public async Task UpdateAttendanceAsync_NonExistingAttendance_ThrowsException()
    {
        // Act & Assert
        var act = async () => await _attendanceService.UpdateAttendanceAsync(999, true, null);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task DeleteAttendanceAsync_ValidId_DeletesAttendance()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var attendance = RehearsalAttendance.Create(rehearsal.Id, "user123");
        _context.RehearsalAttendances.Add(attendance);
        await _context.SaveChangesAsync();

        // Act
        await _attendanceService.DeleteAttendanceAsync(attendance.Id);

        // Assert
        var deleted = await _context.RehearsalAttendances.FindAsync(attendance.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAttendanceAsync_NonExistingAttendance_ThrowsException()
    {
        // Act & Assert
        var act = async () => await _attendanceService.DeleteAttendanceAsync(999);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task GetUserAttendanceCountAsync_ReturnsCorrectCount()
    {
        // Arrange
        var rehearsal1 = Rehearsal.Create(DateTime.Now.AddDays(-10), "Location 1");
        var rehearsal2 = Rehearsal.Create(DateTime.Now.AddDays(-5), "Location 2");
        var rehearsal3 = Rehearsal.Create(DateTime.Now.AddDays(-2), "Location 3");
        _context.Rehearsals.AddRange(rehearsal1, rehearsal2, rehearsal3);
        await _context.SaveChangesAsync();

        var userId = "user123";
        var attended1 = RehearsalAttendance.Create(rehearsal1.Id, userId);
        attended1.MarkAttendance(true); // Approve
        var attended2 = RehearsalAttendance.Create(rehearsal2.Id, userId);
        attended2.MarkAttendance(true); // Approve
        var notAttended = RehearsalAttendance.Create(rehearsal3.Id, userId);
        notAttended.MarkAttendance(false); // Not approved

        _context.RehearsalAttendances.AddRange(attended1, attended2, notAttended);
        await _context.SaveChangesAsync();

        var startDate = DateTime.Now.AddDays(-15);
        var endDate = DateTime.Now;

        // Act
        var result = await _attendanceService.GetUserAttendanceCountAsync(userId, startDate, endDate);

        // Assert
        result.Should().Be(2); // Only attended/approved rehearsals count
    }

    [Fact]
    public async Task GetAttendanceStatsAsync_ReturnsCorrectStats()
    {
        // Arrange
        var rehearsal1 = Rehearsal.Create(DateTime.Now.AddDays(-10), "Location 1");
        var rehearsal2 = Rehearsal.Create(DateTime.Now.AddDays(-5), "Location 2");
        _context.Rehearsals.AddRange(rehearsal1, rehearsal2);
        await _context.SaveChangesAsync();

        var att1 = RehearsalAttendance.Create(rehearsal1.Id, "user1");
        att1.MarkAttendance(true); // Approve
        var att2 = RehearsalAttendance.Create(rehearsal2.Id, "user1");
        att2.MarkAttendance(true); // Approve
        var att3 = RehearsalAttendance.Create(rehearsal1.Id, "user2");
        att3.MarkAttendance(true); // Approve
        _context.RehearsalAttendances.AddRange(att1, att2, att3);
        await _context.SaveChangesAsync();

        var startDate = DateTime.Now.AddDays(-15);
        var endDate = DateTime.Now;

        // Act
        var result = await _attendanceService.GetAttendanceStatsAsync(startDate, endDate);

        // Assert
        result.Should().HaveCount(2);
        result["user1"].Should().Be(2);
        result["user2"].Should().Be(1);
    }

    [Fact]
    public async Task GetAttendanceStatsAsync_ExcludesNotAttended()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(-5), "Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var attended = RehearsalAttendance.Create(rehearsal.Id, "user1");
        attended.MarkAttendance(true); // Approve
        var notAttended = RehearsalAttendance.Create(rehearsal.Id, "user2");
        notAttended.MarkAttendance(false); // Not approved

        _context.RehearsalAttendances.AddRange(attended, notAttended);
        await _context.SaveChangesAsync();

        var startDate = DateTime.Now.AddDays(-10);
        var endDate = DateTime.Now;

        // Act
        var result = await _attendanceService.GetAttendanceStatsAsync(startDate, endDate);

        // Assert
        result.Should().HaveCount(1);
        result.Should().ContainKey("user1");
        result.Should().NotContainKey("user2");
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
