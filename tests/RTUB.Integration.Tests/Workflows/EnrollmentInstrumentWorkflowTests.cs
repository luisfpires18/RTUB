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
/// Comprehensive integration tests for event enrollment with multi-instrument support
/// Tests cover instrument selection, OtherInstruments tracking, and counter calculations
/// </summary>
public class EnrollmentInstrumentWorkflowTests : IntegrationTestBase
{
    public EnrollmentInstrumentWorkflowTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Enrollment_WithPrimaryInstrument_CountsCorrectly()
    {
        // Arrange
        var user = await CreateUserWithInstrumentsAsync("user1",
            (InstrumentType.Guitarra, true),
            (InstrumentType.Bandolim, false));
        var eventEntity = await CreateEventAsync();

        // Act - Enroll with primary instrument
        using (var scope = Factory.Services.CreateScope())
        {
            var enrollmentService = scope.ServiceProvider.GetRequiredService<IEnrollmentService>();
            await enrollmentService.CreateEnrollmentAsync(user.Id, eventEntity.Id,
                instrument: InstrumentType.Guitarra,
                otherInstruments: "Bandolim",
                willAttend: true);
        }

        // Assert
        using (var scope = Factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var memberInstrumentService = scope.ServiceProvider.GetRequiredService<IMemberInstrumentService>();

            var updatedEvent = await context.Events
                .Include(e => e.Enrollments)
                .FirstAsync(e => e.Id == eventEntity.Id);

            var memberInstruments = await memberInstrumentService.GetMemberInstrumentsByUserIdsAsync(
                new[] { user.Id });

            // Selected instrument should be counted in "Instrumentos"
            var instrumentCounts = updatedEvent.GetPrimaryInstrumentCounts(memberInstruments);
            instrumentCounts.Should().ContainKey(InstrumentType.Guitarra);
            instrumentCounts[InstrumentType.Guitarra].Should().Be(1);

            // Other instruments should be counted in "Outros Instrumentos"
            var otherCounts = updatedEvent.GetOtherInstrumentCounts(memberInstruments);
            otherCounts.Should().ContainKey(InstrumentType.Bandolim);
            otherCounts[InstrumentType.Bandolim].Should().Be(1);
        }
    }

    [Fact]
    public async Task Enrollment_WithNonPrimaryInstrument_CountsInPrimaryCategoryNotOther()
    {
        // Arrange
        var user = await CreateUserWithInstrumentsAsync("user2",
            (InstrumentType.Guitarra, true),
            (InstrumentType.Bandolim, false),
            (InstrumentType.Cavaquinho, false));
        var eventEntity = await CreateEventAsync();

        // Act - Enroll with non-primary instrument (Bandolim)
        using (var scope = Factory.Services.CreateScope())
        {
            var enrollmentService = scope.ServiceProvider.GetRequiredService<IEnrollmentService>();
            await enrollmentService.CreateEnrollmentAsync(user.Id, eventEntity.Id,
                instrument: InstrumentType.Bandolim,
                otherInstruments: "Guitarra, Cavaquinho",
                willAttend: true);
        }

        // Assert
        using (var scope = Factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var memberInstrumentService = scope.ServiceProvider.GetRequiredService<IMemberInstrumentService>();

            var updatedEvent = await context.Events
                .Include(e => e.Enrollments)
                .FirstAsync(e => e.Id == eventEntity.Id);

            var memberInstruments = await memberInstrumentService.GetMemberInstrumentsByUserIdsAsync(
                new[] { user.Id });

            // Bandolim (non-primary) should still be counted in "Instrumentos"
            var instrumentCounts = updatedEvent.GetPrimaryInstrumentCounts(memberInstruments);
            instrumentCounts.Should().ContainKey(InstrumentType.Bandolim);
            instrumentCounts[InstrumentType.Bandolim].Should().Be(1);
            instrumentCounts.Should().NotContainKey(InstrumentType.Guitarra);

            // Primary instrument (not selected) should be in "Outros"
            var otherCounts = updatedEvent.GetOtherInstrumentCounts(memberInstruments);
            otherCounts.Should().ContainKey(InstrumentType.Guitarra);
            otherCounts[InstrumentType.Guitarra].Should().Be(1);
            otherCounts.Should().ContainKey(InstrumentType.Cavaquinho);
            otherCounts[InstrumentType.Cavaquinho].Should().Be(1);
        }
    }

    [Fact]
    public async Task Enrollment_WantToPlayFalse_DoesNotCountInstruments()
    {
        // Arrange
        var user = await CreateUserWithInstrumentsAsync("user3",
            (InstrumentType.Guitarra, true));
        var eventEntity = await CreateEventAsync();

        // Act - Enroll without wanting to play
        using (var scope = Factory.Services.CreateScope())
        {
            var enrollmentService = scope.ServiceProvider.GetRequiredService<IEnrollmentService>();
            await enrollmentService.CreateEnrollmentAsync(user.Id, eventEntity.Id,
                instrument: null,
                otherInstruments: null,
                willAttend: false);
        }

        // Assert - No instruments should be counted
        using (var scope = Factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var memberInstrumentService = scope.ServiceProvider.GetRequiredService<IMemberInstrumentService>();

            var updatedEvent = await context.Events
                .Include(e => e.Enrollments)
                .FirstAsync(e => e.Id == eventEntity.Id);

            var memberInstruments = await memberInstrumentService.GetMemberInstrumentsByUserIdsAsync(
                new[] { user.Id });

            var instrumentCounts = updatedEvent.GetPrimaryInstrumentCounts(memberInstruments);
            instrumentCounts.Should().BeEmpty();

            var otherCounts = updatedEvent.GetOtherInstrumentCounts(memberInstruments);
            otherCounts.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task Enrollment_MultipleUsersWithDifferentInstruments_CountsAllCorrectly()
    {
        // Arrange
        var user1 = await CreateUserWithInstrumentsAsync("user4",
            (InstrumentType.Guitarra, true), (InstrumentType.Bandolim, false));
        var user2 = await CreateUserWithInstrumentsAsync("user5",
            (InstrumentType.Cavaquinho, true), (InstrumentType.Guitarra, false));
        var user3 = await CreateUserWithInstrumentsAsync("user6",
            (InstrumentType.Bandolim, true), (InstrumentType.Flauta, false));
        var eventEntity = await CreateEventAsync();

        // Act - Multiple enrollments
        using (var scope = Factory.Services.CreateScope())
        {
            var enrollmentService = scope.ServiceProvider.GetRequiredService<IEnrollmentService>();

            // User1 plays Guitarra (primary)
            await enrollmentService.CreateEnrollmentAsync(user1.Id, eventEntity.Id,
                InstrumentType.Guitarra, notes: null, willAttend: true, otherInstruments: "Bandolim");

            // User2 plays Guitarra (non-primary)
            await enrollmentService.CreateEnrollmentAsync(user2.Id, eventEntity.Id,
                InstrumentType.Guitarra, notes: null, willAttend: true, otherInstruments: "Cavaquinho");

            // User3 plays Bandolim (primary)
            await enrollmentService.CreateEnrollmentAsync(user3.Id, eventEntity.Id,
                InstrumentType.Bandolim, notes: null, willAttend: true, otherInstruments: "Flauta");
        }

        // Assert
        using (var scope = Factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var memberInstrumentService = scope.ServiceProvider.GetRequiredService<IMemberInstrumentService>();

            var updatedEvent = await context.Events
                .Include(e => e.Enrollments)
                .FirstAsync(e => e.Id == eventEntity.Id);

            var memberInstruments = await memberInstrumentService.GetMemberInstrumentsByUserIdsAsync(
                new[] { user1.Id, user2.Id, user3.Id });

            // "Instrumentos" counter
            var instrumentCounts = updatedEvent.GetPrimaryInstrumentCounts(memberInstruments);
            instrumentCounts[InstrumentType.Guitarra].Should().Be(2); // User1 + User2
            instrumentCounts[InstrumentType.Bandolim].Should().Be(1); // User3

            // "Outros Instrumentos" counter
            var otherCounts = updatedEvent.GetOtherInstrumentCounts(memberInstruments);
            otherCounts[InstrumentType.Bandolim].Should().Be(1); // User1's other
            otherCounts[InstrumentType.Cavaquinho].Should().Be(1); // User2's other
            otherCounts[InstrumentType.Flauta].Should().Be(1); // User3's other
        }
    }

    [Fact]
    public async Task Enrollment_UserWithNoInstruments_CanEnrollWithoutPlaying()
    {
        // Arrange
        var user = await CreateUserAsync("user7");
        var eventEntity = await CreateEventAsync();

        // Act - Enroll without instruments
        using (var scope = Factory.Services.CreateScope())
        {
            var enrollmentService = scope.ServiceProvider.GetRequiredService<IEnrollmentService>();
            await enrollmentService.CreateEnrollmentAsync(user.Id, eventEntity.Id,
                instrument: null,
                otherInstruments: null,
                willAttend: false);
        }

        // Assert - Should succeed and not count anything
        using (var scope = Factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var enrollment = await context.Enrollments
                .FirstOrDefaultAsync(e => e.EventId == eventEntity.Id && e.UserId == user.Id);

            enrollment.Should().NotBeNull();
            enrollment!.Instrument.Should().BeNull();
            enrollment.OtherInstruments.Should().BeNullOrEmpty();
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

    #endregion
}
