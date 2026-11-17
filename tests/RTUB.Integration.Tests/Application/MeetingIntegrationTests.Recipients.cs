// MeetingIntegrationTests: Recipients tests.
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

public partial class MeetingIntegrationTests
{
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
}
