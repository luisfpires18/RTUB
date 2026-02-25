using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Repositories;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Repositories;

/// <summary>
/// Unit tests for RehearsalAttendanceRepository
/// Tests repository layer operations including batch operations
/// </summary>
public class RehearsalAttendanceRepositoryTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly RehearsalAttendanceRepository _repository;

    public RehearsalAttendanceRepositoryTests(DatabaseFixture fixture)
    {
        // Clean database at constructor start to ensure test isolation
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();

        _context = _fixture.CreateContext();
        _repository = new RehearsalAttendanceRepository(_fixture.CreateContextFactory());
    }

    [Fact]
    public async Task DeleteByRehearsalIdAsync_WithNoAttendances_DoesNotThrow()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        // Act
        var act = async () => await _repository.DeleteByRehearsalIdAsync(rehearsal.Id);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeleteByRehearsalIdAsync_WithSingleAttendance_DeletesAttendance()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var attendance = RehearsalAttendance.Create(rehearsal.Id, "user123");
        _context.RehearsalAttendances.Add(attendance);
        await _context.SaveChangesAsync();

        // Act
        await _repository.DeleteByRehearsalIdAsync(rehearsal.Id);

        // Assert
        var remainingAttendances = await _context.RehearsalAttendances
            .Where(a => a.RehearsalId == rehearsal.Id)
            .ToListAsync();
        remainingAttendances.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteByRehearsalIdAsync_WithMultipleAttendances_DeletesAllForRehearsal()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var attendance1 = RehearsalAttendance.Create(rehearsal.Id, "user1");
        var attendance2 = RehearsalAttendance.Create(rehearsal.Id, "user2");
        var attendance3 = RehearsalAttendance.Create(rehearsal.Id, "user3");
        _context.RehearsalAttendances.AddRange(attendance1, attendance2, attendance3);
        await _context.SaveChangesAsync();

        // Act
        await _repository.DeleteByRehearsalIdAsync(rehearsal.Id);

        // Assert
        var remainingAttendances = await _context.RehearsalAttendances
            .Where(a => a.RehearsalId == rehearsal.Id)
            .ToListAsync();
        remainingAttendances.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteByRehearsalIdAsync_DoesNotDeleteOtherRehearsalAttendances()
    {
        // Arrange
        var rehearsal1 = Rehearsal.Create(DateTime.Now.AddDays(7), "Location 1");
        var rehearsal2 = Rehearsal.Create(DateTime.Now.AddDays(14), "Location 2");
        _context.Rehearsals.AddRange(rehearsal1, rehearsal2);
        await _context.SaveChangesAsync();

        var attendance1 = RehearsalAttendance.Create(rehearsal1.Id, "user1");
        var attendance2 = RehearsalAttendance.Create(rehearsal1.Id, "user2");
        var attendance3 = RehearsalAttendance.Create(rehearsal2.Id, "user1");
        _context.RehearsalAttendances.AddRange(attendance1, attendance2, attendance3);
        await _context.SaveChangesAsync();

        // Act
        await _repository.DeleteByRehearsalIdAsync(rehearsal1.Id);

        // Assert
        var rehearsal1Attendances = await _context.RehearsalAttendances
            .Where(a => a.RehearsalId == rehearsal1.Id)
            .ToListAsync();
        var rehearsal2Attendances = await _context.RehearsalAttendances
            .Where(a => a.RehearsalId == rehearsal2.Id)
            .ToListAsync();

        rehearsal1Attendances.Should().BeEmpty();
        rehearsal2Attendances.Should().HaveCount(1);
        rehearsal2Attendances[0].UserId.Should().Be("user1");
    }

    [Fact]
    public async Task DeleteByRehearsalIdAsync_WithNonExistentRehearsalId_DoesNotThrow()
    {
        // Arrange - No attendances exist
        var nonExistentRehearsalId = 9999;

        // Act
        var act = async () => await _repository.DeleteByRehearsalIdAsync(nonExistentRehearsalId);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeleteByRehearsalIdAsync_PreservesAttendanceWithDifferentInstruments()
    {
        // Arrange
        var rehearsal1 = Rehearsal.Create(DateTime.Now.AddDays(7), "Location 1");
        var rehearsal2 = Rehearsal.Create(DateTime.Now.AddDays(14), "Location 2");
        _context.Rehearsals.AddRange(rehearsal1, rehearsal2);
        await _context.SaveChangesAsync();

        var attendance1 = RehearsalAttendance.Create(rehearsal1.Id, "user1", InstrumentType.Guitarra);
        attendance1.Notes = "Notes for rehearsal 1";
        var attendance2 = RehearsalAttendance.Create(rehearsal2.Id, "user1", InstrumentType.Bandolim);
        attendance2.Notes = "Notes for rehearsal 2";
        _context.RehearsalAttendances.AddRange(attendance1, attendance2);
        await _context.SaveChangesAsync();

        // Act
        await _repository.DeleteByRehearsalIdAsync(rehearsal1.Id);

        // Assert
        var remainingAttendance = await _context.RehearsalAttendances
            .FirstOrDefaultAsync(a => a.RehearsalId == rehearsal2.Id);
        remainingAttendance.Should().NotBeNull();
        remainingAttendance!.Instrument.Should().Be(InstrumentType.Bandolim);
        remainingAttendance.Notes.Should().Be("Notes for rehearsal 2");
    }

    [Fact]
    public async Task DeleteByRehearsalIdAsync_DeletesAttendancesWithDifferentStatuses()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        var attendance1 = RehearsalAttendance.Create(rehearsal.Id, "user1");
        attendance1.Attended = true;
        attendance1.WillAttend = true;
        var attendance2 = RehearsalAttendance.Create(rehearsal.Id, "user2");
        attendance2.Attended = false;
        attendance2.WillAttend = true;
        var attendance3 = RehearsalAttendance.Create(rehearsal.Id, "user3");
        attendance3.Attended = false;
        attendance3.WillAttend = false;
        _context.RehearsalAttendances.AddRange(attendance1, attendance2, attendance3);
        await _context.SaveChangesAsync();

        // Act
        await _repository.DeleteByRehearsalIdAsync(rehearsal.Id);

        // Assert
        var remainingAttendances = await _context.RehearsalAttendances
            .Where(a => a.RehearsalId == rehearsal.Id)
            .ToListAsync();
        remainingAttendances.Should().BeEmpty();
    }

    public void Dispose()
    {
        _fixture.CleanDatabase(_context).GetAwaiter().GetResult();
        _context.Dispose();
    }
}
