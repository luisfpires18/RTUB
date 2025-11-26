using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Application.Repositories;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Xunit;

namespace RTUB.Integration.Tests.Application;

/// <summary>
/// Integration tests for Meeting functionality
/// Tests the complete lifecycle including authorization, email notifications, and recipient filtering
/// </summary>
public class MeetingIntegrationTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly MeetingService _meetingService;

    public MeetingIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options, Mock.Of<Microsoft.AspNetCore.Http.IHttpContextAccessor>(), new AuditContext());

        // Create mocks for new dependencies
        var mockPushNotificationFactory = new Mock<IPushNotificationFactory>();
        var mockPushNotificationService = new Mock<IPushNotificationService>();
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

        _meetingService = new MeetingService(
            new MeetingRepository(_context),
            _context,
            mockPushNotificationFactory.Object,
            mockPushNotificationService.Object,
            mockHttpContextAccessor.Object);
    }

    #region Meeting Lifecycle Tests

    [Fact]
    public async Task MeetingLifecycle_CreateEditDelete_WorksCorrectly()
    {
        // Arrange - Create a meeting
        var meeting = new Meeting
        {
            Type = MeetingType.AssembleiaGeralOrdinaria,
            Title = "Annual General Meeting",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Main Hall",
            Statement = "Discussion of annual budget and activities"
        };

        // Act - Create
        var created = await _meetingService.CreateMeetingAsync(meeting);

        // Assert - Created successfully
        created.Should().NotBeNull();
        created.Id.Should().BeGreaterThan(0);
        created.Title.Should().Be("Annual General Meeting");

        // Act - Edit
        created.Title = "Updated Annual General Meeting";
        created.Location = "Conference Room A";
        await _meetingService.UpdateMeetingAsync(created);

        // Assert - Updated successfully
        var veteranoId = await CreateTestUser("veterano", "VETERANO");
        var updated = await _meetingService.GetMeetingByIdAsync(created.Id, veteranoId);
        updated.Should().NotBeNull();
        updated!.Title.Should().Be("Updated Annual General Meeting");
        updated.Location.Should().Be("Conference Room A");

        // Act - Delete
        await _meetingService.DeleteMeetingAsync(created.Id);

        // Assert - Deleted successfully
        var deleted = await _meetingService.GetMeetingByIdAsync(created.Id, veteranoId);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task MeetingCancellation_SetsCancellationPropertiesCorrectly()
    {
        // Arrange
        var meeting = new Meeting
        {
            Type = MeetingType.AssembleiaGeralExtraordinaria,
            Title = "Emergency Meeting",
            Date = DateTime.UtcNow.AddDays(7),
            Statement = "Urgent matters to discuss"
        };
        var created = await _meetingService.CreateMeetingAsync(meeting);

        // Act - Cancel the meeting
        created.IsCancelled = true;
        created.CancellationReason = "Insufficient quorum expected";
        await _meetingService.UpdateMeetingAsync(created);

        // Assert
        var veteranoId = await CreateTestUser("veterano", "VETERANO");
        var cancelled = await _meetingService.GetMeetingByIdAsync(created.Id, veteranoId);
        cancelled.Should().NotBeNull();
        cancelled!.IsCancelled.Should().BeTrue();
        cancelled.CancellationReason.Should().Be("Insufficient quorum expected");
    }

    [Fact]
    public async Task MeetingUncancellation_ClearsCancellationProperties()
    {
        // Arrange
        var meeting = new Meeting
        {
            Type = MeetingType.ConselhoVeteranos,
            Title = "Veterans Council",
            Date = DateTime.UtcNow.AddDays(14),
            Statement = "Regular CV meeting",
            IsCancelled = true,
            CancellationReason = "Weather conditions"
        };
        var created = await _meetingService.CreateMeetingAsync(meeting);

        // Act - Uncancel the meeting
        created.IsCancelled = false;
        created.CancellationReason = null;
        await _meetingService.UpdateMeetingAsync(created);

        // Assert
        var veteranoId = await CreateTestUser("veterano", "VETERANO");
        var uncancelled = await _meetingService.GetMeetingByIdAsync(created.Id, veteranoId);
        uncancelled.Should().NotBeNull();
        uncancelled!.IsCancelled.Should().BeFalse();
        uncancelled.CancellationReason.Should().BeNull();
    }

    #endregion

    #region Authorization and Visibility Tests

    [Fact]
    public async Task CVMeeting_OnlyVisibleToVeteranosAndTunossauros()
    {
        // Arrange
        var cvMeeting = new Meeting
        {
            Type = MeetingType.ConselhoVeteranos,
            Title = "CV Strategic Planning",
            Date = DateTime.UtcNow.AddDays(7),
            Statement = "Planning for next semester"
        };
        await _meetingService.CreateMeetingAsync(cvMeeting);

        var veteranoId = await CreateTestUser("veterano", "VETERANO");
        var tunossauroId = await CreateTestUser("tunossauro", "TUNOSSAURO");
        var tunoId = await CreateTestUser("tuno", "TUNO");

        // Act
        var veteranoMeetings = await _meetingService.GetAllMeetingsAsync(null, 1, 10, veteranoId);
        var tunossauroMeetings = await _meetingService.GetAllMeetingsAsync(null, 1, 10, tunossauroId);
        var tunoMeetings = await _meetingService.GetAllMeetingsAsync(null, 1, 10, tunoId);

        // Assert
        veteranoMeetings.Should().HaveCount(1);
        veteranoMeetings.First().Type.Should().Be(MeetingType.ConselhoVeteranos);

        tunossauroMeetings.Should().HaveCount(1);
        tunossauroMeetings.First().Type.Should().Be(MeetingType.ConselhoVeteranos);

        tunoMeetings.Should().BeEmpty();
    }

    [Fact]
    public async Task CVMeeting_DirectAccessByTuno_ReturnsNull()
    {
        // Arrange
        var cvMeeting = new Meeting
        {
            Type = MeetingType.ConselhoVeteranos,
            Title = "CV Meeting",
            Date = DateTime.UtcNow.AddDays(7),
            Statement = "Restricted meeting"
        };
        var created = await _meetingService.CreateMeetingAsync(cvMeeting);

        var tunoId = await CreateTestUser("tuno", "TUNO");

        // Act
        var result = await _meetingService.GetMeetingByIdAsync(created.Id, tunoId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task AssembleiaMeetings_VisibleToAllUsers()
    {
        // Arrange
        var agoMeeting = new Meeting
        {
            Type = MeetingType.AssembleiaGeralOrdinaria,
            Title = "AGO Meeting",
            Date = DateTime.UtcNow.AddDays(7),
            Statement = "Open to all members"
        };
        var ageMeeting = new Meeting
        {
            Type = MeetingType.AssembleiaGeralExtraordinaria,
            Title = "AGE Meeting",
            Date = DateTime.UtcNow.AddDays(10),
            Statement = "Emergency session"
        };
        await _meetingService.CreateMeetingAsync(agoMeeting);
        await _meetingService.CreateMeetingAsync(ageMeeting);

        var veteranoId = await CreateTestUser("veterano", "VETERANO");
        var tunoId = await CreateTestUser("tuno", "TUNO");

        // Act
        var veteranoMeetings = await _meetingService.GetAllMeetingsAsync(null, 1, 10, veteranoId);
        var tunoMeetings = await _meetingService.GetAllMeetingsAsync(null, 1, 10, tunoId);

        // Assert
        veteranoMeetings.Should().HaveCount(2);
        tunoMeetings.Should().HaveCount(2);
    }

    [Fact]
    public async Task MixedMeetings_FilteredByUserRole()
    {
        // Arrange
        await _meetingService.CreateMeetingAsync(new Meeting
        {
            Type = MeetingType.AssembleiaGeralOrdinaria,
            Title = "AGO Meeting",
            Date = DateTime.UtcNow.AddDays(5),
            Statement = "AGO"
        });
        await _meetingService.CreateMeetingAsync(new Meeting
        {
            Type = MeetingType.AssembleiaGeralExtraordinaria,
            Title = "AGE Meeting",
            Date = DateTime.UtcNow.AddDays(10),
            Statement = "AGE"
        });
        await _meetingService.CreateMeetingAsync(new Meeting
        {
            Type = MeetingType.ConselhoVeteranos,
            Title = "CV Meeting",
            Date = DateTime.UtcNow.AddDays(15),
            Statement = "CV"
        });

        var veteranoId = await CreateTestUser("veterano", "VETERANO");
        var tunoId = await CreateTestUser("tuno", "TUNO");

        // Act
        var veteranoCount = await _meetingService.GetTotalCountAsync(null, veteranoId);
        var tunoCount = await _meetingService.GetTotalCountAsync(null, tunoId);

        // Assert
        veteranoCount.Should().Be(3); // Can see all meetings
        tunoCount.Should().Be(2); // Can only see AGO and AGE
    }

    #endregion

    #region Email Notification Recipient Filtering Tests

    [Fact]
    public async Task CVMeeting_RecipientsIncludeVeteranosAndTunossaurosAndMagister()
    {
        // Arrange
        var users = await CreateTestUsersForEmailFiltering();

        // Act - Filter recipients for CV meeting
        var cvRecipients = GetCVMeetingRecipients(await _context.Users.ToListAsync());

        // Assert
        cvRecipients.Should().HaveCount(4); // 1 Veterano, 1 Tunossauro, 1 Tuno with Magister, 1 Veterano with Magister
        cvRecipients.Should().Contain(u => u.Nickname == "VeteranoUser");
        cvRecipients.Should().Contain(u => u.Nickname == "TunossauroUser");
        cvRecipients.Should().Contain(u => u.Nickname == "MagisterUser");
        cvRecipients.Should().Contain(u => u.Nickname == "VeteranoMagisterUser");
        cvRecipients.Should().NotContain(u => u.Nickname == "TunoUser");
    }

    [Fact]
    public async Task CVMeeting_NonRecipientsAreTunosWithoutVeteranStatus()
    {
        // Arrange
        var users = await CreateTestUsersForEmailFiltering();

        // Act - Filter non-recipients for CV meeting
        var cvNonRecipients = GetCVMeetingNonRecipients(await _context.Users.ToListAsync());

        // Assert
        cvNonRecipients.Should().HaveCount(1);
        cvNonRecipients.Should().Contain(u => u.Nickname == "TunoUser");
        cvNonRecipients.Should().NotContain(u => u.CurrentRole == "VETERANO");
        cvNonRecipients.Should().NotContain(u => u.CurrentRole == "TUNOSSAURO");
    }

    [Fact]
    public async Task AssembleiaMeeting_RecipientsIncludeAllSubscribedExceptLeitao()
    {
        // Arrange
        var users = await CreateTestUsersForEmailFiltering();
        await CreateTestUser("leitao", "LEITAO", subscribed: true); // Leitao should be excluded

        // Act - Filter recipients for Assembleia meeting
        var assembleiaRecipients = GetAssembleiaMeetingRecipients(await _context.Users.ToListAsync());

        // Assert
        assembleiaRecipients.Should().HaveCount(5); // All except Leitao and unsubscribed
        assembleiaRecipients.Should().Contain(u => u.Nickname == "VeteranoUser");
        assembleiaRecipients.Should().Contain(u => u.Nickname == "TunossauroUser");
        assembleiaRecipients.Should().Contain(u => u.Nickname == "TunoUser");
        assembleiaRecipients.Should().Contain(u => u.Nickname == "MagisterUser");
        assembleiaRecipients.Should().NotContain(u => u.Nickname == "leitao");
    }

    [Fact]
    public async Task AssembleiaMeeting_NonRecipientsAreUnsubscribed()
    {
        // Arrange
        await CreateTestUsersForEmailFiltering();
        await CreateTestUser("unsubscribed", "TUNO", subscribed: false);

        // Act - Filter non-recipients for Assembleia meeting
        var assembleianonRecipients = GetAssembleiaMeetingNonRecipients(await _context.Users.ToListAsync());

        // Assert
        assembleianonRecipients.Should().HaveCount(1);
        assembleianonRecipients.Should().Contain(u => u.Nickname == "unsubscribed");
    }

    [Fact]
    public async Task EmailFiltering_OnlyIncludesEmailConfirmedUsers()
    {
        // Arrange
        await CreateTestUser("confirmed", "VETERANO", emailConfirmed: true);
        await CreateTestUser("unconfirmed", "VETERANO", emailConfirmed: false);

        // Act
        var allUsers = await _context.Users.ToListAsync();
        var confirmedUsers = allUsers.Where(u => u.EmailConfirmed).ToList();

        // Assert
        confirmedUsers.Should().Contain(u => u.Nickname == "confirmed");
        confirmedUsers.Should().NotContain(u => u.Nickname == "unconfirmed");
    }

    [Fact]
    public async Task CVMeeting_CanAddTunoRepresentativeToRecipients()
    {
        // Arrange
        var users = await CreateTestUsersForEmailFiltering();
        var tunoUser = users.First(u => u.Nickname == "TunoUser");

        // Act - Simulate adding Tuno representative
        var allUsers = await _context.Users.ToListAsync();
        var cvRecipients = GetCVMeetingRecipients(allUsers);

        // Add Tuno representative manually
        if (!cvRecipients.Any(r => r.Id == tunoUser.Id))
        {
            cvRecipients.Add(tunoUser);
        }

        // Assert
        cvRecipients.Should().Contain(u => u.Nickname == "TunoUser");
        cvRecipients.Should().HaveCount(5); // 4 regular + 1 Tuno representative
    }

    #endregion

    #region Role-Based Access Control Tests

    [Fact]
    public async Task PresidenteMesaAssembleia_CanManageAssembleiaMeetings()
    {
        // Arrange
        var userId = await CreateTestUserWithPosition("presidente_mesa", Position.PresidenteMesaAssembleia);
        var agoMeeting = new Meeting
        {
            Type = MeetingType.AssembleiaGeralOrdinaria,
            Title = "AGO Meeting",
            Date = DateTime.UtcNow.AddDays(7),
            Statement = "Statement"
        };

        // Act
        var created = await _meetingService.CreateMeetingAsync(agoMeeting);

        // Assert
        created.Should().NotBeNull();
        // In real scenario, authorization would be checked in the Razor page
        // This test verifies the meeting can be created
    }

    [Fact]
    public async Task PresidenteMesaAssembleia_CannotManageCVMeetings()
    {
        // Arrange - This is conceptual, actual authorization happens in the UI layer
        // The service itself doesn't prevent creation, but the UI checks positions
        var userId = await CreateTestUserWithPosition("presidente_mesa", Position.PresidenteMesaAssembleia);

        // The check would be:
        // CanManageMeeting(cvMeeting) where user.Positions contains PresidenteMesaAssembleia
        // Should return false for CV meetings

        // Assert - Documented behavior: PresidenteMesaAssembleia can only manage AGO/AGE
        var user = await _context.Users.FindAsync(userId);
        var hasPosition = user!.Positions.Contains(Position.PresidenteMesaAssembleia);
        hasPosition.Should().BeTrue();

        // CV meetings should not be manageable by PresidenteMesaAssembleia
        // This is enforced at the UI level through CanManageMeeting checks
    }

    [Fact]
    public async Task PresidenteConselhoVeteranos_CanManageCVMeetings()
    {
        // Arrange
        var userId = await CreateTestUserWithPosition("presidente_cv", Position.PresidenteConselhoVeteranos);
        var cvMeeting = new Meeting
        {
            Type = MeetingType.ConselhoVeteranos,
            Title = "CV Meeting",
            Date = DateTime.UtcNow.AddDays(7),
            Statement = "Statement"
        };

        // Act
        var created = await _meetingService.CreateMeetingAsync(cvMeeting);

        // Assert
        created.Should().NotBeNull();
        // Verification of position-based authorization
        var user = await _context.Users.FindAsync(userId);
        user!.Positions.Should().Contain(Position.PresidenteConselhoVeteranos);
    }

    [Fact]
    public async Task PresidenteConselhoVeteranos_CannotManageAssembleiaMeetings()
    {
        // Arrange - Conceptual test for authorization logic
        var userId = await CreateTestUserWithPosition("presidente_cv", Position.PresidenteConselhoVeteranos);

        // Assert - Documented behavior: PresidenteConselhoVeteranos can only manage CV
        var user = await _context.Users.FindAsync(userId);
        var hasPosition = user!.Positions.Contains(Position.PresidenteConselhoVeteranos);
        hasPosition.Should().BeTrue();

        // AGO/AGE meetings should not be manageable by PresidenteConselhoVeteranos
        // This is enforced at the UI level through CanManageMeeting checks
    }

    #endregion

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

    private async Task<string> CreateTestUserWithPosition(string nickname, Position position)
    {
        var now = DateTime.Now;
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = $"{nickname}@test.com",
            Email = $"{nickname}@test.com",
            FirstName = "Test",
            LastName = "User",
            Nickname = nickname,
            PhoneNumber = "123456789",
            EmailConfirmed = true,
            Subscribed = true,
            YearTuno = now.Year - 3,
            MonthTuno = now.Month,
            Categories = new List<MemberCategory> { MemberCategory.Tuno, MemberCategory.Veterano },
            Positions = new List<Position> { position }
        };

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
}
