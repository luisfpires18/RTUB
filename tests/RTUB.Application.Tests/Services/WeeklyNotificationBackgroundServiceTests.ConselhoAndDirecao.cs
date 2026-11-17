// WeeklyNotificationBackgroundService: conselho and direcao.
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

public partial class WeeklyNotificationBackgroundServiceTests
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
    public void CanUserSeeMeeting_ReuniaoDirecao_AdminWithTunoCategory_ReturnsTrue()
    {
        // Arrange
        var meeting = new Meeting { Type = MeetingType.ReuniaoDirecao };
        var user = CreateUserWithRole("TUNO");
        user.Categories = new List<MemberCategory> { MemberCategory.Tuno };
        user.Positions = new List<Position>();

        // Act
        var result = WeeklyNotificationBackgroundService.CanUserSeeMeeting(meeting, user, isAdmin: true);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CanUserSeeMeeting_ReuniaoDirecao_AdminWithoutTunoCategory_ReturnsFalse()
    {
        // Arrange
        var meeting = new Meeting { Type = MeetingType.ReuniaoDirecao };
        var user = CreateUserWithRole("TUNO");
        user.Categories = new List<MemberCategory> { MemberCategory.Caloiro };
        user.Positions = new List<Position>();

        // Act
        var result = WeeklyNotificationBackgroundService.CanUserSeeMeeting(meeting, user, isAdmin: true);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CanUserSeeMeeting_ReuniaoDirecao_NonAdminTuno_ReturnsFalse()
    {
        // Arrange
        var meeting = new Meeting { Type = MeetingType.ReuniaoDirecao };
        var user = CreateUserWithRole("TUNO");
        user.Categories = new List<MemberCategory> { MemberCategory.Tuno };
        user.Positions = new List<Position>();

        // Act — isAdmin defaults to false
        var result = WeeklyNotificationBackgroundService.CanUserSeeMeeting(meeting, user);

        // Assert
        Assert.False(result);
    }

    #endregion
}
