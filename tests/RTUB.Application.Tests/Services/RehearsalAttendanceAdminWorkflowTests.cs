using FluentAssertions;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Tests for admin-specific rehearsal attendance workflows
/// Tests the workflow of admins adding members directly to rehearsal attendance
/// </summary>
public class RehearsalAttendanceAdminWorkflowTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly RehearsalAttendanceService _attendanceService;

    public RehearsalAttendanceAdminWorkflowTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options, Mock.Of<Microsoft.AspNetCore.Http.IHttpContextAccessor>(), new AuditContext());
        _attendanceService = new RehearsalAttendanceService(_context);
    }

    #region Admin Add Member Workflow Tests

    [Fact]
    public async Task AdminAddMember_ShouldCreateAttendanceAndMarkAsAttended()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var userId = "admin-added-user";
        var instrument = InstrumentType.Guitarra;

        // Act - Simulate admin workflow: Mark attendance then immediately approve
        var attendance = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, true, instrument);
        await _attendanceService.UpdateAttendanceAsync(attendance.Id, true, instrument);

        // Assert
        var result = await _attendanceService.GetAttendanceByIdAsync(attendance.Id);
        result.Should().NotBeNull();
        result!.RehearsalId.Should().Be(rehearsal.Id);
        result.UserId.Should().Be(userId);
        result.Attended.Should().BeTrue("Admin-added members should be marked as attended");
        result.Instrument.Should().Be(instrument);
    }

    [Fact]
    public async Task AdminAddMember_WithoutInstrument_ShouldStillBeMarkedAttended()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(8), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var userId = "user-no-instrument";

        // Act - Admin adds member without specifying instrument
        var attendance = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, true, null);
        await _attendanceService.UpdateAttendanceAsync(attendance.Id, true, null);

        // Assert
        var result = await _attendanceService.GetAttendanceByIdAsync(attendance.Id);
        result.Should().NotBeNull();
        result!.Attended.Should().BeTrue();
        result.Instrument.Should().BeNull();
    }

    [Fact]
    public async Task AdminAddExistingMember_ShouldUpdateExistingAttendance()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(9), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var userId = "existing-user";
        
        // Member already marked attendance (pending)
        var existingAttendance = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, true, InstrumentType.Bandolim);

        // Act - Admin tries to add the same member
        var attendance = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, true, InstrumentType.Guitarra);
        await _attendanceService.UpdateAttendanceAsync(attendance.Id, true, InstrumentType.Guitarra);

        // Assert
        attendance.Id.Should().Be(existingAttendance.Id, "Should update existing attendance, not create new");
        attendance.Instrument.Should().Be(InstrumentType.Guitarra, "Should update instrument");
        
        var result = await _attendanceService.GetAttendanceByIdAsync(attendance.Id);
        result!.Attended.Should().BeTrue("Should be marked as attended by admin");
    }

    [Fact]
    public async Task AdminAddMultipleMembers_AllShouldBeMarkedAttended()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(10), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var users = new[] { "user1", "user2", "user3", "user4", "user5" };

        // Act - Admin adds multiple members
        foreach (var userId in users)
        {
            var attendance = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, true, null);
            await _attendanceService.UpdateAttendanceAsync(attendance.Id, true, null);
        }

        // Assert
        var allAttendances = await _attendanceService.GetAttendancesByRehearsalIdAsync(rehearsal.Id);
        allAttendances.Should().HaveCount(5);
        allAttendances.Should().OnlyContain(a => a.Attended == true, "All admin-added members should be marked attended");
    }

    [Fact]
    public async Task AdminAddMember_ThenDeleteAttendance_ShouldRemoveFromList()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(11), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var userId = "temp-user";
        var attendance = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, true, null);
        await _attendanceService.UpdateAttendanceAsync(attendance.Id, true, null);

        // Act - Admin deletes the attendance
        await _attendanceService.DeleteAttendanceAsync(attendance.Id);

        // Assert
        var result = await _attendanceService.GetAttendanceByIdAsync(attendance.Id);
        result.Should().BeNull("Attendance should be deleted");

        var allAttendances = await _attendanceService.GetAttendancesByRehearsalIdAsync(rehearsal.Id);
        allAttendances.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAttendancesByRehearsalId_ShouldSeparateAttendedFromPending()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(12), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        // Admin adds 3 members (pre-approved)
        for (int i = 0; i < 3; i++)
        {
            var att = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, $"admin-user-{i}", true, null);
            await _attendanceService.UpdateAttendanceAsync(att.Id, true, null);
        }

        // Regular users mark attendance (pending)
        for (int i = 0; i < 2; i++)
        {
            await _attendanceService.MarkAttendanceAsync(rehearsal.Id, $"regular-user-{i}", true, null);
        }

        // Act
        var allAttendances = await _attendanceService.GetAttendancesByRehearsalIdAsync(rehearsal.Id);

        // Assert
        allAttendances.Should().HaveCount(5);
        allAttendances.Where(a => a.Attended).Should().HaveCount(3, "Admin-added members");
        allAttendances.Where(a => !a.Attended).Should().HaveCount(2, "Pending members");
    }

    #endregion

    #region Instrument Handling Tests

    [Fact]
    public async Task AdminAddMember_WithDifferentInstrument_ShouldUpdateInstrument()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(13), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var userId = "user-change-instrument";

        // Member marked with Bandolim
        var attendance1 = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, true, InstrumentType.Bandolim);

        // Act - Admin updates with Guitarra
        var attendance2 = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, true, InstrumentType.Guitarra);
        await _attendanceService.UpdateAttendanceAsync(attendance2.Id, true, InstrumentType.Guitarra);

        // Assert
        attendance2.Id.Should().Be(attendance1.Id);
        attendance2.Instrument.Should().Be(InstrumentType.Guitarra);
    }

    [Fact]
    public async Task AdminUpdateAttendance_ShouldPreserveInstrument()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(14), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var userId = "user-preserve-instrument";
        var instrument = InstrumentType.Bandolim;

        var attendance = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, true, instrument);

        // Act - Update attended status without changing instrument
        await _attendanceService.UpdateAttendanceAsync(attendance.Id, true, instrument);

        // Assert
        var result = await _attendanceService.GetAttendanceByIdAsync(attendance.Id);
        result!.Instrument.Should().Be(instrument);
        result.Attended.Should().BeTrue();
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task UpdateNonExistentAttendance_ShouldThrowException()
    {
        // Act & Assert
        var act = async () => await _attendanceService.UpdateAttendanceAsync(99999, true, null);
        
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task DeleteNonExistentAttendance_ShouldThrowException()
    {
        // Act & Assert
        var act = async () => await _attendanceService.DeleteAttendanceAsync(99999);
        
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    #endregion

    #region Data Consistency Tests

    [Fact]
    public async Task MultipleAdminsAddingSameMember_ShouldNotCreateDuplicates()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(15), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var userId = "contested-user";

        // Act - Simulate two admins trying to add same member
        var attendance1 = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, true, InstrumentType.Guitarra);
        await _attendanceService.UpdateAttendanceAsync(attendance1.Id, true, InstrumentType.Guitarra);

        var attendance2 = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, true, InstrumentType.Bandolim);
        await _attendanceService.UpdateAttendanceAsync(attendance2.Id, true, InstrumentType.Bandolim);

        // Assert
        attendance1.Id.Should().Be(attendance2.Id, "Should update existing record, not create duplicate");

        var allAttendances = await _context.RehearsalAttendances
            .Where(a => a.RehearsalId == rehearsal.Id && a.UserId == userId)
            .ToListAsync();
        
        allAttendances.Should().HaveCount(1, "Should only have one record per user per rehearsal");
        allAttendances[0].Instrument.Should().Be(InstrumentType.Bandolim, "Should have latest instrument");
    }

    [Fact]
    public async Task GetAttendancesByRehearsalId_ShouldOrderByCheckedInAt()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(16), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        // Add members with slight delays to ensure different timestamps
        var userIds = new[] { "user-a", "user-b", "user-c" };
        foreach (var userId in userIds)
        {
            var att = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, true, null);
            await _attendanceService.UpdateAttendanceAsync(att.Id, true, null);
            await Task.Delay(10); // Small delay to ensure different timestamps
        }

        // Act
        var attendances = (await _attendanceService.GetAttendancesByRehearsalIdAsync(rehearsal.Id)).ToList();

        // Assert
        attendances.Should().HaveCount(3);
        // Should be ordered by CheckedInAt
        for (int i = 0; i < attendances.Count - 1; i++)
        {
            attendances[i].CheckedInAt.Should().BeOnOrBefore(attendances[i + 1].CheckedInAt);
        }
    }

    #endregion

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
