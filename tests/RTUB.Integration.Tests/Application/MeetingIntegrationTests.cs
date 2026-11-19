using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Application.Repositories;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Integration.Tests.Application;

/// <summary>
/// Integration tests for Meeting functionality
/// Tests the complete lifecycle including authorization, email notifications, and recipient filtering
/// </summary>
public partial class MeetingIntegrationTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly MeetingService _meetingService;

    public MeetingIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options, Mock.Of<Microsoft.AspNetCore.Http.IHttpContextAccessor>(), new AuditContext(), new RTUB.Application.Services.AuditLogAppender());

        var factory = WrapInFactory(options);

        // Create mocks for new dependencies
        var mockPushNotificationFactory = new Mock<IPushNotificationFactory>();
        var mockPushNotificationService = new Mock<IPushNotificationService>();

        _meetingService = new MeetingService(
            new MeetingRepository(factory),
            factory,
            mockPushNotificationFactory.Object,
            mockPushNotificationService.Object);
    }

    #region Email Recipient Filtering Helper Methods

    /// <summary>
    /// Gets recipients for CV meetings: Veterano + Tunossauro + Magister
    /// </summary>
    private List<ApplicationUser> GetCVMeetingRecipients(List<ApplicationUser> allUsers)
    {
        return allUsers
            .Where(u => u.Subscribed && u.EmailConfirmed &&
                   (u.CurrentRole == "VETERANO" || u.CurrentRole == "TUNOSSAURO" || u.Positions.Contains(Position.Magister)))
            .OrderBy(u => u.Nickname ?? u.FirstName)
            .ToList();
    }

    /// <summary>
    /// Gets non-recipients for CV meetings: Tuno category members who aren't veteran/tunossauro and don't have Magister position
    /// </summary>
    private List<ApplicationUser> GetCVMeetingNonRecipients(List<ApplicationUser> allUsers)
    {
        return allUsers
            .Where(u => u.EmailConfirmed &&
                   u.IsTuno() &&
                   u.CurrentRole == "TUNO" && // Not Veterano or Tunossauro yet
                   !u.Positions.Contains(Position.Magister))
            .OrderBy(u => u.Nickname ?? u.FirstName)
            .ToList();
    }

    /// <summary>
    /// Gets recipients for Assembleia meetings: All subscribed members except Leitão
    /// </summary>
    private List<ApplicationUser> GetAssembleiaMeetingRecipients(List<ApplicationUser> allUsers)
    {
        return allUsers
            .Where(u => u.Subscribed && u.EmailConfirmed && !u.IsLeitao())
            .OrderBy(u => u.Nickname ?? u.FirstName)
            .ToList();
    }

    /// <summary>
    /// Gets non-recipients for Assembleia meetings: Unsubscribed members
    /// </summary>
    private List<ApplicationUser> GetAssembleiaMeetingNonRecipients(List<ApplicationUser> allUsers)
    {
        return allUsers
            .Where(u => u.EmailConfirmed && !u.Subscribed)
            .OrderBy(u => u.Nickname ?? u.FirstName)
            .ToList();
    }

    #endregion

    #region Helper Methods

    private async Task<string> CreateTestUser(string nickname, string role, bool subscribed = true, bool emailConfirmed = true)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = $"{nickname}@test.com",
            Email = $"{nickname}@test.com",
            FirstName = "Test",
            LastName = "User",
            Nickname = nickname,
            PhoneNumber = "123456789",
            EmailConfirmed = emailConfirmed,
            Subscribed = subscribed
        };

        // Set YearTuno and MonthTuno based on desired role
        var now = DateTime.Now;
        switch (role)
        {
            case "VETERANO":
                user.YearTuno = now.Year - 3; // 3 years as Tuno -> Veterano
                user.MonthTuno = now.Month;
                user.Categories = new List<MemberCategory> { MemberCategory.Tuno, MemberCategory.Veterano };
                break;
            case "TUNOSSAURO":
                user.YearTuno = now.Year - 7; // 7 years as Tuno -> Tunossauro
                user.MonthTuno = now.Month;
                user.Categories = new List<MemberCategory> { MemberCategory.Tuno, MemberCategory.Veterano, MemberCategory.Tunossauro };
                break;
            case "TUNO":
                user.YearTuno = now.Year - 1; // 1 year as Tuno
                user.MonthTuno = now.Month;
                user.Categories = new List<MemberCategory> { MemberCategory.Tuno };
                break;
            case "LEITAO":
                user.YearLeitao = now.Year;
                user.MonthLeitao = now.Month;
                user.YearTuno = null;
                user.MonthTuno = null;
                user.Categories = new List<MemberCategory> { MemberCategory.Leitao };
                break;
        }

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user.Id;
    }

    private async Task<List<ApplicationUser>> CreateTestUsersForEmailFiltering()
    {
        var users = new List<ApplicationUser>();
        var now = DateTime.Now;

        // Veterano user
        var veterano = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "veterano@test.com",
            Email = "veterano@test.com",
            FirstName = "Veterano",
            LastName = "User",
            Nickname = "VeteranoUser",
            PhoneNumber = "111111111",
            EmailConfirmed = true,
            Subscribed = true,
            YearTuno = now.Year - 3,
            MonthTuno = now.Month,
            Categories = new List<MemberCategory> { MemberCategory.Tuno, MemberCategory.Veterano }
        };

        // Tunossauro user
        var tunossauro = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "tunossauro@test.com",
            Email = "tunossauro@test.com",
            FirstName = "Tunossauro",
            LastName = "User",
            Nickname = "TunossauroUser",
            PhoneNumber = "222222222",
            EmailConfirmed = true,
            Subscribed = true,
            YearTuno = now.Year - 7,
            MonthTuno = now.Month,
            Categories = new List<MemberCategory> { MemberCategory.Tuno, MemberCategory.Veterano, MemberCategory.Tunossauro }
        };

        // Tuno user (not Veterano/Tunossauro)
        var tuno = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "tuno@test.com",
            Email = "tuno@test.com",
            FirstName = "Tuno",
            LastName = "User",
            Nickname = "TunoUser",
            PhoneNumber = "333333333",
            EmailConfirmed = true,
            Subscribed = true,
            YearTuno = now.Year - 1,
            MonthTuno = now.Month,
            Categories = new List<MemberCategory> { MemberCategory.Tuno }
        };

        // Tuno with Magister position (should receive CV emails)
        var magister = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "magister@test.com",
            Email = "magister@test.com",
            FirstName = "Magister",
            LastName = "User",
            Nickname = "MagisterUser",
            PhoneNumber = "444444444",
            EmailConfirmed = true,
            Subscribed = true,
            YearTuno = now.Year - 1,
            MonthTuno = now.Month,
            Categories = new List<MemberCategory> { MemberCategory.Tuno },
            Positions = new List<Position> { Position.Magister }
        };

        // Veterano with Magister position
        var veteranoMagister = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "vetmagister@test.com",
            Email = "vetmagister@test.com",
            FirstName = "VeteranoMagister",
            LastName = "User",
            Nickname = "VeteranoMagisterUser",
            PhoneNumber = "555555555",
            EmailConfirmed = true,
            Subscribed = true,
            YearTuno = now.Year - 3,
            MonthTuno = now.Month,
            Categories = new List<MemberCategory> { MemberCategory.Tuno, MemberCategory.Veterano },
            Positions = new List<Position> { Position.Magister }
        };

        users.Add(veterano);
        users.Add(tunossauro);
        users.Add(tuno);
        users.Add(magister);
        users.Add(veteranoMagister);

        _context.Users.AddRange(users);
        await _context.SaveChangesAsync();

        return users;
    }

    #endregion

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private static IDbContextFactory<ApplicationDbContext> WrapInFactory(DbContextOptions<ApplicationDbContext> options)
    {
        var mock = new Mock<IDbContextFactory<ApplicationDbContext>>();
        mock.Setup(f => f.CreateDbContext()).Returns(() => new ApplicationDbContext(options, Mock.Of<Microsoft.AspNetCore.Http.IHttpContextAccessor>(), new AuditContext(), new RTUB.Application.Services.AuditLogAppender()));
        return mock.Object;
    }
}
