using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Xunit;

namespace RTUB.Integration.Tests.Workflows;

/// <summary>
/// Integration tests to verify that when admins manually add members to events or rehearsals,
/// the member's primary instrument is automatically selected.
/// </summary>
public class AdminAddMemberWithPrimaryInstrumentTests : IntegrationTestBase
{
    public AdminAddMemberWithPrimaryInstrumentTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task AdminAddMemberToEvent_WithPrimaryInstrument_ShouldAutoSelectIt()
    {
        // Arrange - Create a user with a primary instrument
        var user = await CreateUserWithInstrumentsAsync("testuser1",
            (InstrumentType.Guitarra, true),    // Primary
            (InstrumentType.Bandolim, false));  // Secondary
        var eventEntity = await CreateEventAsync();

        // Act - Simulate admin adding member (this would be done through Events.razor)
        using (var scope = Factory.Services.CreateScope())
        {
            var enrollmentService = scope.ServiceProvider.GetRequiredService<IEnrollmentService>();
            var memberInstrumentService = scope.ServiceProvider.GetRequiredService<IMemberInstrumentService>();
            
            // Get primary instrument (simulating what Events.razor does)
            var primaryInstrument = await memberInstrumentService.GetPrimaryInstrumentAsync(user.Id);
            var instrumentToUse = primaryInstrument?.InstrumentType;
            
            // Create enrollment with primary instrument
            await enrollmentService.CreateEnrollmentAsync(user.Id, eventEntity.Id, instrumentToUse, null, true);
        }

        // Assert - Verify enrollment has the primary instrument
        using (var scope = Factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            var enrollment = await context.Enrollments
                .FirstOrDefaultAsync(e => e.EventId == eventEntity.Id && e.UserId == user.Id);

            enrollment.Should().NotBeNull();
            enrollment!.Instrument.Should().Be(InstrumentType.Guitarra, 
                "Primary instrument should be automatically selected when admin adds member");
            enrollment.WillAttend.Should().BeTrue();
        }
    }

    [Fact]
    public async Task AdminAddMemberToEvent_WithoutPrimaryInstrument_ShouldHaveNullInstrument()
    {
        // Arrange - Create a user without instruments
        var user = await CreateUserAsync("testuser2");
        var eventEntity = await CreateEventAsync();

        // Act - Simulate admin adding member
        using (var scope = Factory.Services.CreateScope())
        {
            var enrollmentService = scope.ServiceProvider.GetRequiredService<IEnrollmentService>();
            var memberInstrumentService = scope.ServiceProvider.GetRequiredService<IMemberInstrumentService>();
            
            var primaryInstrument = await memberInstrumentService.GetPrimaryInstrumentAsync(user.Id);
            var instrumentToUse = primaryInstrument?.InstrumentType;
            
            await enrollmentService.CreateEnrollmentAsync(user.Id, eventEntity.Id, instrumentToUse, null, true);
        }

        // Assert
        using (var scope = Factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            var enrollment = await context.Enrollments
                .FirstOrDefaultAsync(e => e.EventId == eventEntity.Id && e.UserId == user.Id);

            enrollment.Should().NotBeNull();
            enrollment!.Instrument.Should().BeNull("User has no primary instrument");
        }
    }

    [Fact]
    public async Task AdminAddMemberToRehearsal_WithPrimaryInstrument_ShouldAutoSelectIt()
    {
        // Arrange
        var user = await CreateUserWithInstrumentsAsync("testuser3",
            (InstrumentType.Cavaquinho, true),
            (InstrumentType.Flauta, false));
        var rehearsal = await CreateRehearsalAsync();

        // Act - Simulate admin adding member (via CreateAttendanceWithApprovalAsync)
        using (var scope = Factory.Services.CreateScope())
        {
            var attendanceService = scope.ServiceProvider.GetRequiredService<IRehearsalAttendanceService>();
            var memberInstrumentService = scope.ServiceProvider.GetRequiredService<IMemberInstrumentService>();
            
            var primaryInstrument = await memberInstrumentService.GetPrimaryInstrumentAsync(user.Id);
            var instrumentToUse = primaryInstrument?.InstrumentType;
            
            await attendanceService.CreateAttendanceWithApprovalAsync(rehearsal.Id, user.Id, instrumentToUse, null, null);
        }

        // Assert
        using (var scope = Factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            var attendance = await context.RehearsalAttendances
                .FirstOrDefaultAsync(a => a.RehearsalId == rehearsal.Id && a.UserId == user.Id);

            attendance.Should().NotBeNull();
            attendance!.Instrument.Should().Be(InstrumentType.Cavaquinho,
                "Primary instrument should be automatically selected when admin adds member");
            attendance.Attended.Should().BeTrue();
            attendance.WillAttend.Should().BeTrue();
        }
    }

    [Fact]
    public async Task AdminAddMemberToRehearsal_PendingMode_WithPrimaryInstrument_ShouldAutoSelectIt()
    {
        // Arrange
        var user = await CreateUserWithInstrumentsAsync("testuser4",
            (InstrumentType.Bandolim, true));
        var rehearsal = await CreateRehearsalAsync();

        // Act - Simulate admin adding member (via MarkAttendanceAsync for future rehearsals)
        using (var scope = Factory.Services.CreateScope())
        {
            var attendanceService = scope.ServiceProvider.GetRequiredService<IRehearsalAttendanceService>();
            var memberInstrumentService = scope.ServiceProvider.GetRequiredService<IMemberInstrumentService>();
            
            var primaryInstrument = await memberInstrumentService.GetPrimaryInstrumentAsync(user.Id);
            var instrumentToUse = primaryInstrument?.InstrumentType;
            
            await attendanceService.MarkAttendanceAsync(rehearsal.Id, user.Id, true, instrumentToUse, null);
        }

        // Assert
        using (var scope = Factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            var attendance = await context.RehearsalAttendances
                .FirstOrDefaultAsync(a => a.RehearsalId == rehearsal.Id && a.UserId == user.Id);

            attendance.Should().NotBeNull();
            attendance!.Instrument.Should().Be(InstrumentType.Bandolim,
                "Primary instrument should be automatically selected");
            attendance.WillAttend.Should().BeTrue();
            attendance.Attended.Should().BeFalse("Should be pending for future rehearsals");
        }
    }

    [Fact]
    public async Task AdminAddMemberToRehearsal_WithoutPrimaryInstrument_ShouldHaveNullInstrument()
    {
        // Arrange
        var user = await CreateUserAsync("testuser5");
        var rehearsal = await CreateRehearsalAsync();

        // Act
        using (var scope = Factory.Services.CreateScope())
        {
            var attendanceService = scope.ServiceProvider.GetRequiredService<IRehearsalAttendanceService>();
            var memberInstrumentService = scope.ServiceProvider.GetRequiredService<IMemberInstrumentService>();
            
            var primaryInstrument = await memberInstrumentService.GetPrimaryInstrumentAsync(user.Id);
            var instrumentToUse = primaryInstrument?.InstrumentType;
            
            await attendanceService.CreateAttendanceWithApprovalAsync(rehearsal.Id, user.Id, instrumentToUse, null, null);
        }

        // Assert
        using (var scope = Factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            var attendance = await context.RehearsalAttendances
                .FirstOrDefaultAsync(a => a.RehearsalId == rehearsal.Id && a.UserId == user.Id);

            attendance.Should().NotBeNull();
            attendance!.Instrument.Should().BeNull("User has no primary instrument");
        }
    }

    #region Helper Methods

    private async Task<ApplicationUser> CreateUserAsync(string userId)
    {
        using var scope = Factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            Id = userId,
            UserName = $"user_{userId}",
            Email = $"{userId}@test.com",
            EmailConfirmed = true,
            FirstName = "Test",
            LastName = "User",
            Nickname = $"Nick_{userId}",
            PhoneNumber = "9123123"
        };

        await userManager.CreateAsync(user, "TestPassword123!");
        return user;
    }

    private async Task<ApplicationUser> CreateUserWithInstrumentsAsync(
        string userId,
        params (InstrumentType instrument, bool isPrimary)[] instruments)
    {
        var user = await CreateUserAsync(userId);

        using var scope = Factory.Services.CreateScope();
        var memberInstrumentService = scope.ServiceProvider.GetRequiredService<IMemberInstrumentService>();

        foreach (var (instrument, isPrimary) in instruments)
        {
            await memberInstrumentService.AddInstrumentAsync(user.Id, instrument, isPrimary);
        }

        return user;
    }

    private async Task<Event> CreateEventAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var eventEntity = Event.Create(
            $"Test Event {Guid.NewGuid()}",
            DateTime.Now.AddDays(7),
            "Test Location",
            EventType.Festival,
            "Test Description");

        context.Events.Add(eventEntity);
        await context.SaveChangesAsync();
        return eventEntity;
    }

    private async Task<Rehearsal> CreateRehearsalAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var rehearsal = Rehearsal.Create(
            DateTime.Now.AddDays(7),
            "Test Location");

        context.Rehearsals.Add(rehearsal);
        await context.SaveChangesAsync();
        return rehearsal;
    }

    #endregion
}
