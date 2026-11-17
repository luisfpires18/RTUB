// RehearsalInstrumentWorkflowTests: MultipleUsersAndUpdates tests.
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Enums;

namespace RTUB.Integration.Tests.Workflows;

public partial class RehearsalInstrumentWorkflowTests
{
    [Fact]
    public async Task RehearsalAttendance_MultipleUsersWithDifferentInstruments_CountsAllCorrectly()
    {
        // Arrange
        var user1 = await CreateUserWithInstrumentsAsync("ruser4",
            (InstrumentType.Guitarra, true), (InstrumentType.Bandolim, false));
        var user2 = await CreateUserWithInstrumentsAsync("ruser5",
            (InstrumentType.Cavaquinho, true), (InstrumentType.Guitarra, false));
        var user3 = await CreateUserWithInstrumentsAsync("ruser6",
            (InstrumentType.Bandolim, true), (InstrumentType.Flauta, false));
        var rehearsal = await CreateRehearsalAsync();

        // Act - Multiple attendances
        using (var scope = Factory.Services.CreateScope())
        {
            var attendanceService = scope.ServiceProvider.GetRequiredService<IRehearsalAttendanceService>();

            // User1 plays Guitarra (primary)
            await attendanceService.MarkAttendanceAsync(rehearsal.Id, user1.Id,
                true, InstrumentType.Guitarra, null, "Bandolim");

            // User2 plays Guitarra (non-primary)
            await attendanceService.MarkAttendanceAsync(rehearsal.Id, user2.Id,
                true, InstrumentType.Guitarra, null, "Cavaquinho");

            // User3 plays Bandolim (primary)
            await attendanceService.MarkAttendanceAsync(rehearsal.Id, user3.Id,
                true, InstrumentType.Bandolim, null, "Flauta");
        }

        // Assert
        using (var scope = Factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var memberInstrumentService = scope.ServiceProvider.GetRequiredService<IMemberInstrumentService>();

            var updatedRehearsal = await context.Rehearsals
                .Include(r => r.Attendances)
                .FirstAsync(r => r.Id == rehearsal.Id);

            var memberInstruments = await memberInstrumentService.GetMemberInstrumentsByUserIdsAsync(
                new[] { user1.Id, user2.Id, user3.Id });

            // "Instrumentos" counter
            var instrumentCounts = updatedRehearsal.GetPrimaryInstrumentCounts(memberInstruments);
            instrumentCounts[InstrumentType.Guitarra].Should().Be(2); // User1 + User2
            instrumentCounts[InstrumentType.Bandolim].Should().Be(1); // User3

            // "Outros Instrumentos" counter
            var otherCounts = updatedRehearsal.GetOtherInstrumentCounts(memberInstruments);
            otherCounts[InstrumentType.Bandolim].Should().Be(1); // User1's other
            otherCounts[InstrumentType.Cavaquinho].Should().Be(1); // User2's other
            otherCounts[InstrumentType.Flauta].Should().Be(1); // User3's other
        }
    }

    [Fact]
    public async Task RehearsalAttendance_UserWithNoInstruments_CanAttendWithoutPlaying()
    {
        // Arrange
        var user = await CreateUserAsync("ruser7");
        var rehearsal = await CreateRehearsalAsync();

        // Act - Mark attendance without instruments
        using (var scope = Factory.Services.CreateScope())
        {
            var attendanceService = scope.ServiceProvider.GetRequiredService<IRehearsalAttendanceService>();
            await attendanceService.MarkAttendanceAsync(rehearsal.Id, user.Id,
                willAttend: true,
                instrument: null,
                notes: null,
                otherInstruments: null);
        }

        // Assert - Should succeed and not count anything
        using (var scope = Factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var attendance = await context.RehearsalAttendances
                .FirstOrDefaultAsync(a => a.RehearsalId == rehearsal.Id && a.UserId == user.Id);

            attendance.Should().NotBeNull();
            attendance!.Instrument.Should().BeNull();
            attendance.OtherInstruments.Should().BeNullOrEmpty();
        }
    }

    [Fact]
    public async Task RehearsalAttendance_UpdateInstrument_UpdatesCountsCorrectly()
    {
        // Arrange
        var user = await CreateUserWithInstrumentsAsync("ruser8",
            (InstrumentType.Guitarra, true),
            (InstrumentType.Bandolim, false));
        var rehearsal = await CreateRehearsalAsync();

        // Act - Initial attendance
        using (var scope = Factory.Services.CreateScope())
        {
            var attendanceService = scope.ServiceProvider.GetRequiredService<IRehearsalAttendanceService>();
            await attendanceService.MarkAttendanceAsync(rehearsal.Id, user.Id,
                true, InstrumentType.Guitarra, null, "Bandolim");
        }

        // Act - Update to different instrument
        using (var scope = Factory.Services.CreateScope())
        {
            var attendanceService = scope.ServiceProvider.GetRequiredService<IRehearsalAttendanceService>();
            await attendanceService.MarkAttendanceAsync(rehearsal.Id, user.Id,
                true, InstrumentType.Bandolim, null, "Guitarra");
        }

        // Assert - New instrument should be counted
        using (var scope = Factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var memberInstrumentService = scope.ServiceProvider.GetRequiredService<IMemberInstrumentService>();

            var updatedRehearsal = await context.Rehearsals
                .Include(r => r.Attendances)
                .FirstAsync(r => r.Id == rehearsal.Id);

            var memberInstruments = await memberInstrumentService.GetMemberInstrumentsByUserIdsAsync(
                new[] { user.Id });

            var instrumentCounts = updatedRehearsal.GetPrimaryInstrumentCounts(memberInstruments);
            instrumentCounts.Should().ContainKey(InstrumentType.Bandolim);
            instrumentCounts[InstrumentType.Bandolim].Should().Be(1);
            instrumentCounts.Should().NotContainKey(InstrumentType.Guitarra);

            var otherCounts = updatedRehearsal.GetOtherInstrumentCounts(memberInstruments);
            otherCounts.Should().ContainKey(InstrumentType.Guitarra);
            otherCounts[InstrumentType.Guitarra].Should().Be(1);
        }
    }
}
