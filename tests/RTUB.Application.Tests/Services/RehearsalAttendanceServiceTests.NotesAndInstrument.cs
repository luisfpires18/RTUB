// RehearsalAttendanceService: notes and instrument.
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Repositories;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Application.Tests.Utilities;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

public partial class RehearsalAttendanceServiceTests
{
    [Fact]
    public async Task MarkAttendanceAsync_WithNotes_PersistsNotes()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var userId = "user123";
        var instrument = InstrumentType.Guitarra;
        var notes = "These are my notes for the rehearsal";

        // Act
        var result = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, true, instrument, notes);

        // Assert
        result.Should().NotBeNull();
        result.Notes.Should().Be(notes);

        // Verify in database
        var fromDb = await _context.RehearsalAttendances.FirstOrDefaultAsync(a => a.Id == result.Id);
        fromDb.Should().NotBeNull();
        fromDb!.Notes.Should().Be(notes);
    }

    [Fact]
    public async Task MarkAttendanceAsync_UpdateExistingNotes_UpdatesCorrectly()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var userId = "user123";
        var instrument = InstrumentType.Guitarra;
        var initialNotes = "Initial notes";
        var updatedNotes = "Updated notes after change";

        // Create initial attendance with notes
        var initial = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, true, instrument, initialNotes);
        initial.Notes.Should().Be(initialNotes);

        // Act - Update with new notes
        var updated = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, true, instrument, updatedNotes);

        // Assert
        updated.Id.Should().Be(initial.Id); // Same attendance record
        updated.Notes.Should().Be(updatedNotes);

        // Verify in database
        var fromDb = await _context.RehearsalAttendances.FirstOrDefaultAsync(a => a.Id == initial.Id);
        fromDb.Should().NotBeNull();
        fromDb!.Notes.Should().Be(updatedNotes);
    }

    [Fact]
    public async Task MarkAttendanceAsync_ClearNotes_AllowsClearingNotes()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var userId = "user123";
        var instrument = InstrumentType.Guitarra;
        var initialNotes = "Initial notes";

        // Create initial attendance with notes
        var initial = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, true, instrument, initialNotes);
        initial.Notes.Should().Be(initialNotes);

        // Act - Clear notes by passing null
        var updated = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, true, instrument, null);

        // Assert
        updated.Id.Should().Be(initial.Id); // Same attendance record
        updated.Notes.Should().BeNull();

        // Verify in database
        var fromDb = await _context.RehearsalAttendances.FirstOrDefaultAsync(a => a.Id == initial.Id);
        fromDb.Should().NotBeNull();
        fromDb!.Notes.Should().BeNull();
    }

    [Fact]
    public async Task MarkAttendanceAsync_ClearNotesWithEmptyString_AllowsClearingNotes()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var userId = "user123";
        var instrument = InstrumentType.Guitarra;
        var initialNotes = "Initial notes";

        // Create initial attendance with notes
        var initial = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, true, instrument, initialNotes);
        initial.Notes.Should().Be(initialNotes);

        // Act - Clear notes by passing empty string
        var updated = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, true, instrument, "");

        // Assert
        updated.Id.Should().Be(initial.Id); // Same attendance record
        updated.Notes.Should().Be("");

        // Verify in database
        var fromDb = await _context.RehearsalAttendances.FirstOrDefaultAsync(a => a.Id == initial.Id);
        fromDb.Should().NotBeNull();
        fromDb!.Notes.Should().Be("");
    }

    [Fact]
    public async Task MarkAttendanceAsync_ExistingAttendanceWithInstrument_ClearsInstrumentWhenNullPassed()
    {
        // Arrange - Create attendance with instrument
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var userId = "user123";
        var initialInstrument = InstrumentType.Guitarra;

        // Create attendance with instrument
        var initial = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, true, initialInstrument);
        initial.Instrument.Should().Be(initialInstrument);

        // Act - Clear instrument by passing null (simulates user turning off "Quero tocar" toggle)
        var updated = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, true, null);

        // Assert
        updated.Id.Should().Be(initial.Id); // Same attendance record
        updated.Instrument.Should().BeNull(); // Instrument should be cleared

        // Verify in database
        _context.ChangeTracker.Clear();
        var fromDb = await _context.RehearsalAttendances.FirstOrDefaultAsync(a => a.Id == initial.Id);
        fromDb.Should().NotBeNull();
        fromDb!.Instrument.Should().BeNull();
    }

    [Fact]
    public async Task MarkAttendanceAsync_ToggleOffThenOn_CanClearAndRestoreInstrument()
    {
        // Arrange - Create attendance with instrument
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var userId = "user123";
        var initialInstrument = InstrumentType.Guitarra;
        var newInstrument = InstrumentType.Bandolim;

        // Create attendance with instrument (toggle ON)
        var initial = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, true, initialInstrument);
        initial.Instrument.Should().Be(initialInstrument);

        // Act 1 - Turn toggle OFF (clear instrument)
        var clearedResult = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, true, null);
        clearedResult.Instrument.Should().BeNull();

        // Verify cleared in database
        _context.ChangeTracker.Clear();
        var clearedFromDb = await _context.RehearsalAttendances.FirstOrDefaultAsync(a => a.Id == initial.Id);
        clearedFromDb!.Instrument.Should().BeNull();

        // Act 2 - Turn toggle back ON (set new instrument)
        var restoredResult = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, true, newInstrument);

        // Assert
        restoredResult.Id.Should().Be(initial.Id); // Same attendance record
        restoredResult.Instrument.Should().Be(newInstrument);

        // Verify in database
        _context.ChangeTracker.Clear();
        var restoredFromDb = await _context.RehearsalAttendances.FirstOrDefaultAsync(a => a.Id == initial.Id);
        restoredFromDb.Should().NotBeNull();
        restoredFromDb!.Instrument.Should().Be(newInstrument);
    }

    [Fact]
    public async Task MarkAttendanceAsync_WillAttendFalseWithNullInstrument_ClearsInstrumentCorrectly()
    {
        // Arrange - Create attendance with instrument (user was going and playing)
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var userId = "user123";
        var initialInstrument = InstrumentType.Guitarra;

        // Create attendance - user is going and wants to play
        var initial = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, true, initialInstrument);
        initial.WillAttend.Should().BeTrue();
        initial.Instrument.Should().Be(initialInstrument);

        // Act - User changes to NOT going (and instrument should be cleared)
        var updated = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, false, null, "Can't make it today");

        // Assert
        updated.Id.Should().Be(initial.Id); // Same attendance record
        updated.WillAttend.Should().BeFalse();
        updated.Instrument.Should().BeNull(); // Instrument should be cleared when not attending
        updated.Notes.Should().Be("Can't make it today");

        // Verify in database
        _context.ChangeTracker.Clear();
        var fromDb = await _context.RehearsalAttendances.FirstOrDefaultAsync(a => a.Id == initial.Id);
        fromDb.Should().NotBeNull();
        fromDb!.WillAttend.Should().BeFalse();
        fromDb.Instrument.Should().BeNull();
        fromDb.Notes.Should().Be("Can't make it today");
    }

    [Fact]
    public async Task MarkAttendanceAsync_WhenWillAttendDoesNotChange_DoesNotUpdateCheckedInAt()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var userId = "user-checkin-static";

        var initial = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, true, InstrumentType.Guitarra);
        var originalCheckedInAt = initial.CheckedInAt;

        // Act - call again with same WillAttend value
        var updated = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, true, InstrumentType.Bandolim);

        // Assert
        updated.Id.Should().Be(initial.Id);
        updated.CheckedInAt.Should().Be(originalCheckedInAt, "check-in time should remain the same when WillAttend does not change");
    }

    [Fact]
    public async Task MarkAttendanceAsync_WhenWillAttendChanges_UpdatesCheckedInAt()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var userId = "user-checkin-change";

        var initial = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, true, InstrumentType.Guitarra);
        var originalCheckedInAt = initial.CheckedInAt;

        // Act - toggle attendance to false
        var updated = await _attendanceService.MarkAttendanceAsync(rehearsal.Id, userId, false, null, "Not going");

        // Assert
        updated.Id.Should().Be(initial.Id);
        updated.WillAttend.Should().BeFalse();
        updated.CheckedInAt.Should().BeAfter(originalCheckedInAt, "check-in time should be refreshed when WillAttend changes");
    }
}
