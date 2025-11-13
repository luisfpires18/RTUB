using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RTUB.Application.Data;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Xunit;
using FluentAssertions;

namespace RTUB.Integration.Tests.Workflows;

/// <summary>
/// Integration tests for rehearsal attendance workflows including notes persistence
/// </summary>
public class RehearsalAttendanceWorkflowTests : IntegrationTestBase
{
    private const string TestUserId = "test-user-123";

    public RehearsalAttendanceWorkflowTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task RehearsalAttendance_WithNotes_PersistsThroughFullWorkflow()
    {
        // Arrange - Create a rehearsal
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        using (var scope = Factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Rehearsals.Add(rehearsal);
            await context.SaveChangesAsync();
        }

        var notes = "Integration test notes for rehearsal attendance";

        // Act - Mark attendance with notes
        using (var scope = Factory.Services.CreateScope())
        {
            var attendanceService = scope.ServiceProvider.GetRequiredService<RTUB.Application.Interfaces.IRehearsalAttendanceService>();
            await attendanceService.MarkAttendanceAsync(
                rehearsal.Id, 
                TestUserId, 
                willAttend: true, 
                instrument: InstrumentType.Guitarra, 
                notes: notes);
        }

        // Assert - Verify notes were persisted
        using (var scope = Factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var attendance = await context.RehearsalAttendances
                .FirstOrDefaultAsync(a => a.RehearsalId == rehearsal.Id && a.UserId == TestUserId);

            attendance.Should().NotBeNull();
            attendance!.Notes.Should().Be(notes);
            attendance.WillAttend.Should().BeTrue();
            attendance.Instrument.Should().Be(InstrumentType.Guitarra);
        }
    }

    [Fact]
    public async Task RehearsalAttendance_UpdateNotes_CorrectlyUpdatesExistingAttendance()
    {
        // Arrange - Create a rehearsal and initial attendance
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        using (var scope = Factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Rehearsals.Add(rehearsal);
            await context.SaveChangesAsync();
        }

        var initialNotes = "Initial notes";
        var updatedNotes = "Updated notes after user change";

        // Act - Create initial attendance
        int attendanceId;
        using (var scope = Factory.Services.CreateScope())
        {
            var attendanceService = scope.ServiceProvider.GetRequiredService<RTUB.Application.Interfaces.IRehearsalAttendanceService>();
            var attendance = await attendanceService.MarkAttendanceAsync(
                rehearsal.Id, 
                TestUserId, 
                willAttend: true, 
                instrument: InstrumentType.Baixo, 
                notes: initialNotes);
            attendanceId = attendance.Id;
        }

        // Act - Update attendance with new notes
        using (var scope = Factory.Services.CreateScope())
        {
            var attendanceService = scope.ServiceProvider.GetRequiredService<RTUB.Application.Interfaces.IRehearsalAttendanceService>();
            await attendanceService.MarkAttendanceAsync(
                rehearsal.Id, 
                TestUserId, 
                willAttend: true, 
                instrument: InstrumentType.Baixo, 
                notes: updatedNotes);
        }

        // Assert - Verify notes were updated (not created new record)
        using (var scope = Factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var attendances = await context.RehearsalAttendances
                .Where(a => a.RehearsalId == rehearsal.Id && a.UserId == TestUserId)
                .ToListAsync();

            attendances.Should().HaveCount(1, "should update existing attendance, not create new one");
            attendances[0].Id.Should().Be(attendanceId, "should be the same attendance record");
            attendances[0].Notes.Should().Be(updatedNotes, "notes should be updated");
        }
    }

    [Fact]
    public async Task RehearsalAttendance_ClearNotes_AllowsClearingExistingNotes()
    {
        // Arrange - Create a rehearsal and attendance with notes
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        using (var scope = Factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Rehearsals.Add(rehearsal);
            await context.SaveChangesAsync();
        }

        var initialNotes = "These notes should be clearable";

        // Create attendance with notes
        using (var scope = Factory.Services.CreateScope())
        {
            var attendanceService = scope.ServiceProvider.GetRequiredService<RTUB.Application.Interfaces.IRehearsalAttendanceService>();
            await attendanceService.MarkAttendanceAsync(
                rehearsal.Id, 
                TestUserId, 
                willAttend: true, 
                instrument: InstrumentType.Bandolim, 
                notes: initialNotes);
        }

        // Act - Clear notes by passing null
        using (var scope = Factory.Services.CreateScope())
        {
            var attendanceService = scope.ServiceProvider.GetRequiredService<RTUB.Application.Interfaces.IRehearsalAttendanceService>();
            await attendanceService.MarkAttendanceAsync(
                rehearsal.Id, 
                TestUserId, 
                willAttend: true, 
                instrument: InstrumentType.Bandolim, 
                notes: null);
        }

        // Assert - Verify notes were cleared
        using (var scope = Factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var attendance = await context.RehearsalAttendances
                .FirstOrDefaultAsync(a => a.RehearsalId == rehearsal.Id && a.UserId == TestUserId);

            attendance.Should().NotBeNull();
            attendance!.Notes.Should().BeNull("notes should be cleared");
            attendance.Instrument.Should().Be(InstrumentType.Bandolim, "other fields should remain");
        }
    }

    [Fact]
    public async Task RehearsalAttendance_ChangeFromNotAttendingToAttending_PreservesNotes()
    {
        // Arrange - Create a rehearsal
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        using (var scope = Factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Rehearsals.Add(rehearsal);
            await context.SaveChangesAsync();
        }

        var notes = "Can't make it initially";

        // Mark as not attending with notes
        using (var scope = Factory.Services.CreateScope())
        {
            var attendanceService = scope.ServiceProvider.GetRequiredService<RTUB.Application.Interfaces.IRehearsalAttendanceService>();
            await attendanceService.MarkAttendanceAsync(
                rehearsal.Id, 
                TestUserId, 
                willAttend: false, 
                instrument: null, 
                notes: notes);
        }

        var updatedNotes = "Changed my mind, will attend now!";

        // Act - Change to attending with updated notes
        using (var scope = Factory.Services.CreateScope())
        {
            var attendanceService = scope.ServiceProvider.GetRequiredService<RTUB.Application.Interfaces.IRehearsalAttendanceService>();
            await attendanceService.MarkAttendanceAsync(
                rehearsal.Id, 
                TestUserId, 
                willAttend: true, 
                instrument: InstrumentType.Cavaquinho, 
                notes: updatedNotes);
        }

        // Assert - Verify state change and notes update
        using (var scope = Factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var attendance = await context.RehearsalAttendances
                .FirstOrDefaultAsync(a => a.RehearsalId == rehearsal.Id && a.UserId == TestUserId);

            attendance.Should().NotBeNull();
            attendance!.WillAttend.Should().BeTrue("should now be attending");
            attendance.Instrument.Should().Be(InstrumentType.Cavaquinho);
            attendance.Notes.Should().Be(updatedNotes, "notes should be updated");
        }
    }
}
