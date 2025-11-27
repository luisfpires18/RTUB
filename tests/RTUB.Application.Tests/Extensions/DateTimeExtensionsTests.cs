using FluentAssertions;
using RTUB.Application.Extensions;

namespace RTUB.Application.Tests.Extensions;

/// <summary>
/// Unit tests for DateTimeExtensions
/// Tests Portuguese date formatting methods
/// </summary>
public class DateTimeExtensionsTests
{
    [Fact]
    public void ToPortugueseShortDate_FormatsCorrectly()
    {
        // Arrange
        var date = new DateTime(2024, 3, 15);

        // Act
        var result = date.ToPortugueseShortDate();

        // Assert
        result.Should().Be("15/03/2024");
    }

    [Fact]
    public void ToPortugueseShortDate_SingleDigitDayMonth_HasLeadingZeros()
    {
        // Arrange
        var date = new DateTime(2024, 1, 5);

        // Act
        var result = date.ToPortugueseShortDate();

        // Assert
        result.Should().Be("05/01/2024");
    }

    [Fact]
    public void ToPortugueseDateTime_FormatsCorrectly()
    {
        // Arrange
        var date = new DateTime(2024, 3, 15, 14, 30, 0);

        // Act
        var result = date.ToPortugueseDateTime();

        // Assert
        result.Should().Be("15/03/2024 14:30");
    }

    [Fact]
    public void ToPortugueseDateTime_MidnightTime_FormatsCorrectly()
    {
        // Arrange
        var date = new DateTime(2024, 3, 15, 0, 0, 0);

        // Act
        var result = date.ToPortugueseDateTime();

        // Assert
        result.Should().Be("15/03/2024 00:00");
    }

    [Fact]
    public void ToPortugueseFullDateTime_FormatsCorrectly()
    {
        // Arrange
        var date = new DateTime(2024, 3, 15, 14, 30, 45);

        // Act
        var result = date.ToPortugueseFullDateTime();

        // Assert
        result.Should().Be("15/03/2024 14:30:45");
    }

    [Fact]
    public void ToPortugueseShortDate_Nullable_WithValue_FormatsCorrectly()
    {
        // Arrange
        DateTime? date = new DateTime(2024, 3, 15);

        // Act
        var result = date.ToPortugueseShortDate();

        // Assert
        result.Should().Be("15/03/2024");
    }

    [Fact]
    public void ToPortugueseShortDate_Nullable_WithNull_ReturnsEmpty()
    {
        // Arrange
        DateTime? date = null;

        // Act
        var result = date.ToPortugueseShortDate();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ToPortugueseDateTime_Nullable_WithValue_FormatsCorrectly()
    {
        // Arrange
        DateTime? date = new DateTime(2024, 3, 15, 14, 30, 0);

        // Act
        var result = date.ToPortugueseDateTime();

        // Assert
        result.Should().Be("15/03/2024 14:30");
    }

    [Fact]
    public void ToPortugueseDateTime_Nullable_WithNull_ReturnsEmpty()
    {
        // Arrange
        DateTime? date = null;

        // Act
        var result = date.ToPortugueseDateTime();

        // Assert
        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData(DayOfWeek.Monday, "Segunda-feira")]
    [InlineData(DayOfWeek.Tuesday, "Terça-feira")]
    [InlineData(DayOfWeek.Wednesday, "Quarta-feira")]
    [InlineData(DayOfWeek.Thursday, "Quinta-feira")]
    [InlineData(DayOfWeek.Friday, "Sexta-feira")]
    [InlineData(DayOfWeek.Saturday, "Sábado")]
    [InlineData(DayOfWeek.Sunday, "Domingo")]
    public void ToPortugueseWeekdayName_ReturnsCorrectName(DayOfWeek dayOfWeek, string expectedName)
    {
        // Arrange - Find a date that falls on the specified day
        var baseDate = new DateTime(2024, 1, 1);
        while (baseDate.DayOfWeek != dayOfWeek)
        {
            baseDate = baseDate.AddDays(1);
        }

        // Act
        var result = baseDate.ToPortugueseWeekdayName();

        // Assert
        result.Should().Be(expectedName);
    }

    [Fact]
    public void ToPortugueseWeekdayName_StartsWithCapitalLetter()
    {
        // Arrange
        var date = new DateTime(2024, 3, 15); // Friday

        // Act
        var result = date.ToPortugueseWeekdayName();

        // Assert
        result[0].Should().Be(char.ToUpper(result[0]));
    }

    [Theory]
    [InlineData(2024, 12, 31, "31/12/2024")]
    [InlineData(2024, 1, 1, "01/01/2024")]
    [InlineData(2000, 2, 29, "29/02/2000")] // Leap year
    public void ToPortugueseShortDate_EdgeCases_FormatsCorrectly(int year, int month, int day, string expected)
    {
        // Arrange
        var date = new DateTime(year, month, day);

        // Act
        var result = date.ToPortugueseShortDate();

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ToPortugueseFullDateTime_WithSeconds_FormatsCorrectly()
    {
        // Arrange
        var date = new DateTime(2024, 3, 15, 23, 59, 59);

        // Act
        var result = date.ToPortugueseFullDateTime();

        // Assert
        result.Should().Be("15/03/2024 23:59:59");
    }
}
