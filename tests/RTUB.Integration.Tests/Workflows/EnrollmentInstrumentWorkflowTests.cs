using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Integration.Tests.Workflows;

/// <summary>
/// Integration tests for event enrollment instrument fields
/// </summary>
public class EnrollmentInstrumentWorkflowTests : IntegrationTestBase
{
    public EnrollmentInstrumentWorkflowTests(TestWebApplicationFactory factory) : base(factory)
    {
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
