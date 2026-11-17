// RehearsalInstrumentWorkflowTests: attendance without instruments.
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;

namespace RTUB.Integration.Tests.Workflows;

public partial class RehearsalInstrumentWorkflowTests
{
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
}
