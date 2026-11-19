// WeeklyNotificationBackgroundService: visible meeting count.
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

public partial class WeeklyNotificationBackgroundServiceTests
{
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
}
