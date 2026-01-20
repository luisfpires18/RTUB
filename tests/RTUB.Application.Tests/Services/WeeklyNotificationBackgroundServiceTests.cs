using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Xunit;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Tests for the WeeklyNotificationBackgroundService meeting visibility filtering.
/// </summary>
public class WeeklyNotificationBackgroundServiceTests
{
    #region CanUserSeeMeeting Tests

    [Fact]
    public void CanUserSeeMeeting_ConselhoVeteranos_VeteranoUser_ReturnsTrue()
    {
        // Arrange
        var meeting = new Meeting { Type = MeetingType.ConselhoVeteranos };
        var user = CreateUserWithRole("VETERANO");

        // Act
        var result = WeeklyNotificationBackgroundService.CanUserSeeMeeting(meeting, user);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CanUserSeeMeeting_ConselhoVeteranos_TunossauroUser_ReturnsTrue()
    {
        // Arrange
        var meeting = new Meeting { Type = MeetingType.ConselhoVeteranos };
        var user = CreateUserWithRole("TUNOSSAURO");

        // Act
        var result = WeeklyNotificationBackgroundService.CanUserSeeMeeting(meeting, user);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CanUserSeeMeeting_ConselhoVeteranos_MagisterPosition_ReturnsTrue()
    {
        // Arrange
        var meeting = new Meeting { Type = MeetingType.ConselhoVeteranos };
        var user = CreateUserWithRole("TUNO");
        user.Positions = new List<Position> { Position.Magister };

        // Act
        var result = WeeklyNotificationBackgroundService.CanUserSeeMeeting(meeting, user);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CanUserSeeMeeting_ConselhoVeteranos_RegularTuno_ReturnsFalse()
    {
        // Arrange
        var meeting = new Meeting { Type = MeetingType.ConselhoVeteranos };
        var user = CreateUserWithRole("TUNO");

        // Act
        var result = WeeklyNotificationBackgroundService.CanUserSeeMeeting(meeting, user);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CanUserSeeMeeting_ReuniaoDirecao_MagisterPosition_ReturnsTrue()
    {
        // Arrange
        var meeting = new Meeting { Type = MeetingType.ReuniaoDirecao };
        var user = CreateUserWithRole("TUNO");
        user.Positions = new List<Position> { Position.Magister };

        // Act
        var result = WeeklyNotificationBackgroundService.CanUserSeeMeeting(meeting, user);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CanUserSeeMeeting_ReuniaoDirecao_ViceMagisterPosition_ReturnsTrue()
    {
        // Arrange
        var meeting = new Meeting { Type = MeetingType.ReuniaoDirecao };
        var user = CreateUserWithRole("TUNO");
        user.Positions = new List<Position> { Position.ViceMagister };

        // Act
        var result = WeeklyNotificationBackgroundService.CanUserSeeMeeting(meeting, user);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CanUserSeeMeeting_ReuniaoDirecao_SecretarioPosition_ReturnsTrue()
    {
        // Arrange
        var meeting = new Meeting { Type = MeetingType.ReuniaoDirecao };
        var user = CreateUserWithRole("TUNO");
        user.Positions = new List<Position> { Position.Secretario };

        // Act
        var result = WeeklyNotificationBackgroundService.CanUserSeeMeeting(meeting, user);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CanUserSeeMeeting_ReuniaoDirecao_PrimeiroTesoureiroPosition_ReturnsTrue()
    {
        // Arrange
        var meeting = new Meeting { Type = MeetingType.ReuniaoDirecao };
        var user = CreateUserWithRole("TUNO");
        user.Positions = new List<Position> { Position.PrimeiroTesoureiro };

        // Act
        var result = WeeklyNotificationBackgroundService.CanUserSeeMeeting(meeting, user);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CanUserSeeMeeting_ReuniaoDirecao_SegundoTesoureiroPosition_ReturnsTrue()
    {
        // Arrange
        var meeting = new Meeting { Type = MeetingType.ReuniaoDirecao };
        var user = CreateUserWithRole("TUNO");
        user.Positions = new List<Position> { Position.SegundoTesoureiro };

        // Act
        var result = WeeklyNotificationBackgroundService.CanUserSeeMeeting(meeting, user);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CanUserSeeMeeting_ReuniaoDirecao_NonDirecaoPosition_ReturnsFalse()
    {
        // Arrange
        var meeting = new Meeting { Type = MeetingType.ReuniaoDirecao };
        var user = CreateUserWithRole("VETERANO");
        user.Positions = new List<Position> { Position.PresidenteConselhoVeteranos };

        // Act
        var result = WeeklyNotificationBackgroundService.CanUserSeeMeeting(meeting, user);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CanUserSeeMeeting_ReuniaoDirecao_UserWithNoPositions_ReturnsFalse()
    {
        // Arrange
        var meeting = new Meeting { Type = MeetingType.ReuniaoDirecao };
        var user = CreateUserWithRole("TUNO");
        user.Positions = new List<Position>();

        // Act
        var result = WeeklyNotificationBackgroundService.CanUserSeeMeeting(meeting, user);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CanUserSeeMeeting_AssembleiaGeralOrdinaria_LeitaoUser_ReturnsFalse()
    {
        // Arrange
        var meeting = new Meeting { Type = MeetingType.AssembleiaGeralOrdinaria };
        var user = CreateLeitaoUser();

        // Act
        var result = WeeklyNotificationBackgroundService.CanUserSeeMeeting(meeting, user);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CanUserSeeMeeting_AssembleiaGeralOrdinaria_TunoUser_ReturnsTrue()
    {
        // Arrange
        var meeting = new Meeting { Type = MeetingType.AssembleiaGeralOrdinaria };
        var user = CreateUserWithRole("TUNO");
        user.Categories = new List<MemberCategory> { MemberCategory.Tuno };

        // Act
        var result = WeeklyNotificationBackgroundService.CanUserSeeMeeting(meeting, user);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CanUserSeeMeeting_AssembleiaGeralExtraordinaria_LeitaoUser_ReturnsFalse()
    {
        // Arrange
        var meeting = new Meeting { Type = MeetingType.AssembleiaGeralExtraordinaria };
        var user = CreateLeitaoUser();

        // Act
        var result = WeeklyNotificationBackgroundService.CanUserSeeMeeting(meeting, user);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CanUserSeeMeeting_AssembleiaGeralExtraordinaria_CaloiroUser_ReturnsTrue()
    {
        // Arrange
        var meeting = new Meeting { Type = MeetingType.AssembleiaGeralExtraordinaria };
        var user = CreateUserWithRole("TUNO");
        user.Categories = new List<MemberCategory> { MemberCategory.Caloiro };

        // Act
        var result = WeeklyNotificationBackgroundService.CanUserSeeMeeting(meeting, user);

        // Assert
        Assert.True(result);
    }

    #endregion

    #region GetVisibleMeetingCount Tests

    [Fact]
    public void GetVisibleMeetingCount_NoMeetings_ReturnsZero()
    {
        // Arrange
        var meetings = new List<Meeting>();
        var user = CreateUserWithRole("TUNO");

        // Act
        var result = WeeklyNotificationBackgroundService.GetVisibleMeetingCount(meetings, user);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void GetVisibleMeetingCount_AllVisibleMeetings_ReturnsCorrectCount()
    {
        // Arrange
        var meetings = new List<Meeting>
        {
            new Meeting { Type = MeetingType.AssembleiaGeralOrdinaria },
            new Meeting { Type = MeetingType.AssembleiaGeralExtraordinaria }
        };
        var user = CreateUserWithRole("TUNO");
        user.Categories = new List<MemberCategory> { MemberCategory.Tuno };

        // Act
        var result = WeeklyNotificationBackgroundService.GetVisibleMeetingCount(meetings, user);

        // Assert
        Assert.Equal(2, result);
    }

    [Fact]
    public void GetVisibleMeetingCount_MixedMeetings_VeteranoSeesAllExceptDirecao()
    {
        // Arrange
        var meetings = new List<Meeting>
        {
            new Meeting { Type = MeetingType.AssembleiaGeralOrdinaria },
            new Meeting { Type = MeetingType.ConselhoVeteranos },
            new Meeting { Type = MeetingType.ReuniaoDirecao }
        };
        var user = CreateUserWithRole("VETERANO");
        user.Categories = new List<MemberCategory> { MemberCategory.Veterano };

        // Act
        var result = WeeklyNotificationBackgroundService.GetVisibleMeetingCount(meetings, user);

        // Assert - Veterano can see AG and CV, but not Direcao
        Assert.Equal(2, result);
    }

    [Fact]
    public void GetVisibleMeetingCount_MixedMeetings_DirecaoMemberSeesAllMeetings()
    {
        // Arrange
        var meetings = new List<Meeting>
        {
            new Meeting { Type = MeetingType.AssembleiaGeralOrdinaria },
            new Meeting { Type = MeetingType.ConselhoVeteranos },
            new Meeting { Type = MeetingType.ReuniaoDirecao }
        };
        // Magister who is also Veterano - can see all meetings
        var user = CreateUserWithRole("VETERANO");
        user.Categories = new List<MemberCategory> { MemberCategory.Veterano };
        user.Positions = new List<Position> { Position.Magister };

        // Act
        var result = WeeklyNotificationBackgroundService.GetVisibleMeetingCount(meetings, user);

        // Assert - Magister can see all meetings (AG, CV, Direcao)
        Assert.Equal(3, result);
    }

    [Fact]
    public void GetVisibleMeetingCount_MixedMeetings_LeitaoSeesNone()
    {
        // Arrange
        var meetings = new List<Meeting>
        {
            new Meeting { Type = MeetingType.AssembleiaGeralOrdinaria },
            new Meeting { Type = MeetingType.ConselhoVeteranos },
            new Meeting { Type = MeetingType.ReuniaoDirecao }
        };
        var user = CreateLeitaoUser();

        // Act
        var result = WeeklyNotificationBackgroundService.GetVisibleMeetingCount(meetings, user);

        // Assert - Leitao cannot see any meetings
        Assert.Equal(0, result);
    }

    [Fact]
    public void GetVisibleMeetingCount_MixedMeetings_RegularTunoSeesOnlyAG()
    {
        // Arrange
        var meetings = new List<Meeting>
        {
            new Meeting { Type = MeetingType.AssembleiaGeralOrdinaria },
            new Meeting { Type = MeetingType.AssembleiaGeralExtraordinaria },
            new Meeting { Type = MeetingType.ConselhoVeteranos },
            new Meeting { Type = MeetingType.ReuniaoDirecao }
        };
        var user = CreateUserWithRole("TUNO");
        user.Categories = new List<MemberCategory> { MemberCategory.Tuno };

        // Act
        var result = WeeklyNotificationBackgroundService.GetVisibleMeetingCount(meetings, user);

        // Assert - Regular Tuno can only see AG meetings
        Assert.Equal(2, result);
    }

    #endregion

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
