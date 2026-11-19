using Microsoft.AspNetCore.Identity;
using RTUB.Application.Data;
using RTUB.Core.Entities;

namespace RTUB.Integration.Tests.Workflows;

/// <summary>
/// Integration tests for rehearsal attendance instrument fields
/// Mirrors EnrollmentInstrumentWorkflowTests for consistency
/// </summary>
public partial class RehearsalInstrumentWorkflowTests : IntegrationTestBase
{
    public RehearsalInstrumentWorkflowTests(TestWebApplicationFactory factory) : base(factory)
    {
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
