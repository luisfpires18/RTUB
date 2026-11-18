using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Tests for the WeeklyNotificationBackgroundService meeting visibility filtering.
/// </summary>
public partial class WeeklyNotificationBackgroundServiceTests
{
    #region Helper Methods

    /// <summary>
    /// Creates a user with the specified role (simulates CurrentRole property based on YearTuno).
    /// </summary>
    private static ApplicationUser CreateUserWithRole(string role)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "testuser@test.com",
            Email = "testuser@test.com",
            FirstName = "Test",
            LastName = "User",
            Nickname = "TestNick",
            PhoneNumber = "123456789",
            Positions = new List<Position>(),
            Categories = new List<MemberCategory>()
        };

        // Set YearTuno to simulate CurrentRole calculation
        // CurrentRole returns: TUNOSSAURO if 6+ years, VETERANO if 2+ years, TUNO otherwise
        var now = DateTime.Now;

        switch (role)
        {
            case "TUNOSSAURO":
                user.YearTuno = now.Year - 7;
                user.MonthTuno = 1;
                break;
            case "VETERANO":
                user.YearTuno = now.Year - 3;
                user.MonthTuno = 1;
                break;
            case "TUNO":
            default:
                user.YearTuno = now.Year - 1;
                user.MonthTuno = 1;
                break;
        }

        return user;
    }

    /// <summary>
    /// Creates a Leitão user (not an associated member).
    /// </summary>
    private static ApplicationUser CreateLeitaoUser()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "leitao@test.com",
            Email = "leitao@test.com",
            FirstName = "Leitão",
            LastName = "User",
            Nickname = "LeitaoNick",
            PhoneNumber = "123456789",
            Positions = new List<Position>(),
            Categories = new List<MemberCategory> { MemberCategory.Leitao }
        };

        // Leitão doesn't have YearTuno set - they're not yet a Tuno
        return user;
    }

    #endregion
}
