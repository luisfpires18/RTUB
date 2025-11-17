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
/// Comprehensive integration tests for rehearsal attendance with multi-instrument support
/// Tests cover instrument selection, OtherInstruments tracking, and counter calculations
/// Mirrors EnrollmentInstrumentWorkflowTests for consistency
/// </summary>
public class RehearsalInstrumentWorkflowTests : IntegrationTestBase
{
    public RehearsalInstrumentWorkflowTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task RehearsalAttendance_WithPrimaryInstrument_CountsCorrectly()
    {
        // Arrange
        var user = await CreateUserWithInstrumentsAsync("ruser1", 
            (InstrumentType.Guitarra, true), 
            (InstrumentType.Bandolim, false));
        var rehearsal = await CreateRehearsalAsync();

        // Act - Mark attendance with primary instrument
        using (var scope = Factory.Services.CreateScope())
        {
            var attendanceService = scope.ServiceProvider.GetRequiredService<IRehearsalAttendanceService>();
            await attendanceService.MarkAttendanceAsync(rehearsal.Id, user.Id, 
                willAttend: true, 
                instrument: InstrumentType.Guitarra, 
                notes: null, 
                otherInstruments: "Bandolim");
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
                new[] { user.Id });

            // Selected instrument should be counted in "Instrumentos"
            var instrumentCounts = updatedRehearsal.GetPrimaryInstrumentCounts(memberInstruments);
            instrumentCounts.Should().ContainKey(InstrumentType.Guitarra);
            instrumentCounts[InstrumentType.Guitarra].Should().Be(1);

            // Other instruments should be counted in "Outros Instrumentos"
            var otherCounts = updatedRehearsal.GetOtherInstrumentCounts(memberInstruments);
            otherCounts.Should().ContainKey(InstrumentType.Bandolim);
            otherCounts[InstrumentType.Bandolim].Should().Be(1);
        }
    }

    [Fact]
    public async Task RehearsalAttendance_WithNonPrimaryInstrument_CountsInPrimaryCategoryNotOther()
    {
        // Arrange
        var user = await CreateUserWithInstrumentsAsync("ruser2", 
            (InstrumentType.Guitarra, true), 
            (InstrumentType.Bandolim, false),
            (InstrumentType.Cavaquinho, false));
        var rehearsal = await CreateRehearsalAsync();

        // Act - Mark attendance with non-primary instrument (Bandolim)
        using (var scope = Factory.Services.CreateScope())
        {
            var attendanceService = scope.ServiceProvider.GetRequiredService<IRehearsalAttendanceService>();
            await attendanceService.MarkAttendanceAsync(rehearsal.Id, user.Id, 
                willAttend: true, 
                instrument: InstrumentType.Bandolim, 
                notes: null, 
                otherInstruments: "Guitarra, Cavaquinho");
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
                new[] { user.Id });

            // Bandolim (non-primary) should still be counted in "Instrumentos"
            var instrumentCounts = updatedRehearsal.GetPrimaryInstrumentCounts(memberInstruments);
            instrumentCounts.Should().ContainKey(InstrumentType.Bandolim);
            instrumentCounts[InstrumentType.Bandolim].Should().Be(1);
            instrumentCounts.Should().NotContainKey(InstrumentType.Guitarra);

            // Primary instrument (not selected) should be in "Outros"
            var otherCounts = updatedRehearsal.GetOtherInstrumentCounts(memberInstruments);
            otherCounts.Should().ContainKey(InstrumentType.Guitarra);
            otherCounts[InstrumentType.Guitarra].Should().Be(1);
            otherCounts.Should().ContainKey(InstrumentType.Cavaquinho);
            otherCounts[InstrumentType.Cavaquinho].Should().Be(1);
        }
    }

    [Fact]
    public async Task RehearsalAttendance_NotAttending_DoesNotCountInstruments()
    {
        // Arrange
        var user = await CreateUserWithInstrumentsAsync("ruser3", 
            (InstrumentType.Guitarra, true));
        var rehearsal = await CreateRehearsalAsync();

        // Act - Mark attendance as not attending
        using (var scope = Factory.Services.CreateScope())
        {
            var attendanceService = scope.ServiceProvider.GetRequiredService<IRehearsalAttendanceService>();
            await attendanceService.MarkAttendanceAsync(rehearsal.Id, user.Id, 
                willAttend: false, 
                instrument: null, 
                notes: null, 
                otherInstruments: null);
        }

        // Assert - No instruments should be counted
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
            instrumentCounts.Should().BeEmpty();

            var otherCounts = updatedRehearsal.GetOtherInstrumentCounts(memberInstruments);
            otherCounts.Should().BeEmpty();
        }
    }

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
            PhoneContact = "12312312"
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
