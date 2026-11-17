using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Integration.Tests.Workflows;

/// <summary>
/// Comprehensive integration tests for rehearsal attendance with multi-instrument support
/// Tests cover instrument selection, OtherInstruments tracking, and counter calculations
/// Mirrors EnrollmentInstrumentWorkflowTests for consistency
/// </summary>
public partial class RehearsalInstrumentWorkflowTests : IntegrationTestBase
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
            PhoneNumber = "12312312"
        };

        await userManager.CreateAsync(user, TestSecret.NewPassword());
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
