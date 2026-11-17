// RehearsalAttendanceAdminWorkflow: instrument and consistency.
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Repositories;
using RTUB.Application.Services;
using RTUB.Application.Tests.Utilities;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Tests.Services;

public partial class RehearsalAttendanceAdminWorkflowTests
{
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
        var result = await _context.RehearsalAttendances.AsNoTracking().Include(a => a.Rehearsal).FirstOrDefaultAsync(a => a.Id == attendance.Id);
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

        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task DeleteNonExistentAttendance_ShouldThrowException()
    {
        // Act & Assert
        var act = async () => await _attendanceService.DeleteAttendanceAsync(99999);

        await act.Should().ThrowAsync<EntityNotFoundException>()
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

        // Create test users to avoid issues with Include navigation property
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
}
