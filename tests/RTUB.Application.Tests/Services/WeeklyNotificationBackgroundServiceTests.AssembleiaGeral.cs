// WeeklyNotificationBackgroundService: assembleia geral.
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

public partial class WeeklyNotificationBackgroundServiceTests
{
    #region CanUserSeeMeeting Tests

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
}
