// RehearsalAttendanceAdminWorkflow: admin add member.
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

public partial class RehearsalAttendanceAdminWorkflowTests
{
    #region Admin Add Member Workflow Tests

    /// <summary>
    /// Task 072 ("As minhas ações pendentes"): a member's first answer is created pending, with its category snapshot, and
    /// an existing presença - pending, approved or "não vou" - is never touched.
    /// </summary>
    [Fact]
    public async Task AddFirstAttendance_CreatesAPendingRow_AndNeverOverwritesAnExistingOne()
    {
        var past = Rehearsal.Create(DateTime.Now.AddDays(-3), "Test Location");
        var approved = Rehearsal.Create(DateTime.Now.AddDays(-4), "Test Location");
        _context.Rehearsals.AddRange(past, approved);
        await _context.SaveChangesAsync();

        var created = await _attendanceService.AddFirstAttendanceAsync(past.Id, "self-report", true, InstrumentType.Bandolim, "fui", null, skipNotification: true);
        var confirmed = await _attendanceService.MarkAttendanceAsync(approved.Id, "self-report", true, InstrumentType.Guitarra);
        await _attendanceService.UpdateAttendanceAsync(confirmed.Id, true, InstrumentType.Guitarra);

        created.Attended.Should().BeFalse("a self-report waits for approval");
        created.WillAttend.Should().BeTrue();
        await FluentActions.Invoking(() => _attendanceService.AddFirstAttendanceAsync(past.Id, "self-report", false))
            .Should().ThrowAsync<InvalidOperationException>();
        await FluentActions.Invoking(() => _attendanceService.AddFirstAttendanceAsync(approved.Id, "self-report", false))
            .Should().ThrowAsync<InvalidOperationException>();
        (await _context.RehearsalAttendances.AsNoTracking().Include(a => a.Rehearsal).FirstOrDefaultAsync(a => a.Id == created.Id))!.WillAttend.Should().BeTrue("the first answer stays");
        (await _context.RehearsalAttendances.AsNoTracking().Include(a => a.Rehearsal).FirstOrDefaultAsync(a => a.Id == confirmed.Id))!.Attended.Should().BeTrue("approved history stays");
        _mockRetirementStatusService.Verify(s => s.ReconcileMemberStatusAsync(It.IsAny<string>()), Times.Once(), "only the approval reconciles");
    }

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
        var result = await _context.RehearsalAttendances.AsNoTracking().Include(a => a.Rehearsal).FirstOrDefaultAsync(a => a.Id == attendance.Id);
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
        var result = await _context.RehearsalAttendances.AsNoTracking().Include(a => a.Rehearsal).FirstOrDefaultAsync(a => a.Id == attendance.Id);
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

        var result = await _context.RehearsalAttendances.AsNoTracking().Include(a => a.Rehearsal).FirstOrDefaultAsync(a => a.Id == attendance.Id);
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

        // Create test users to avoid issues with Include navigation property
        var testUsers = users.Select(userId => new ApplicationUser
        {
            Id = userId,
            UserName = userId,
            Email = $"{userId}@test.com",
            FirstName = "Test",
            LastName = "User",
            Nickname = userId
        }).ToList();
        _context.Users.AddRange(testUsers);
        await _context.SaveChangesAsync();

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
        var result = await _context.RehearsalAttendances.AsNoTracking().Include(a => a.Rehearsal).FirstOrDefaultAsync(a => a.Id == attendance.Id);
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

        // Create test users to avoid issues with Include navigation property
        var userIds = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            userIds.Add($"admin-user-{i}");
        }
        for (int i = 0; i < 2; i++)
        {
            userIds.Add($"regular-user-{i}");
        }

        var testUsers = userIds.Select(userId => new ApplicationUser
        {
            Id = userId,
            UserName = userId,
            Email = $"{userId}@test.com",
            FirstName = "Test",
            LastName = "User",
            Nickname = userId
        }).ToList();
        _context.Users.AddRange(testUsers);
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
}
