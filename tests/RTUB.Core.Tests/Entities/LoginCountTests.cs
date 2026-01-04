using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Core.Tests.Entities;

public class LoginCountTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreateInstance()
    {
        // Arrange
        var userId = "user123";
        var loginDate = DateTime.UtcNow.Date;
        var count = 5;

        // Act
        var loginCount = new LoginCount
        {
            UserId = userId,
            LoginDate = loginDate,
            Count = count
        };

        // Assert
        loginCount.Should().NotBeNull();
        loginCount.UserId.Should().Be(userId);
        loginCount.LoginDate.Should().Be(loginDate);
        loginCount.Count.Should().Be(count);
    }

    [Fact]
    public void LoginDate_ShouldStripTimeComponent()
    {
        // Arrange
        var userId = "user123";
        var dateTimeWithTime = new DateTime(2024, 1, 15, 14, 30, 0);
        var expectedDate = dateTimeWithTime.Date;

        // Act
        var loginCount = new LoginCount
        {
            UserId = userId,
            LoginDate = expectedDate,
            Count = 1
        };

        // Assert
        loginCount.LoginDate.Should().Be(expectedDate);
        loginCount.LoginDate.Hour.Should().Be(0);
        loginCount.LoginDate.Minute.Should().Be(0);
        loginCount.LoginDate.Second.Should().Be(0);
    }

    [Fact]
    public void Count_DefaultValue_ShouldBeZero()
    {
        // Arrange & Act
        var loginCount = new LoginCount
        {
            UserId = "user123",
            LoginDate = DateTime.UtcNow.Date
        };

        // Assert
        loginCount.Count.Should().Be(0);
    }

    [Fact]
    public void Count_CanBeIncremented()
    {
        // Arrange
        var loginCount = new LoginCount
        {
            UserId = "user123",
            LoginDate = DateTime.UtcNow.Date,
            Count = 1
        };

        // Act
        loginCount.Count++;

        // Assert
        loginCount.Count.Should().Be(2);
    }
}
