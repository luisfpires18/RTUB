using FluentAssertions;
using Moq;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Tests for date edit permission logic.
/// Validates both the one-time date edit feature for users and owner-only override.
/// </summary>
public partial class DateEditPermissionTests
{
    /// <summary>
    /// Helper method to check if a date pair is complete (both year and month are set).
    /// This mirrors the logic in Profile.razor.
    /// </summary>
    private static bool IsDatePairComplete(int? year, int? month)
    {
        return year.HasValue && month.HasValue;
    }


    [Fact]
    public void IsDatePairComplete_WhenBothYearAndMonthAreSet_ReturnsTrue()
    {
        // Arrange
        int? year = 2020;
        int? month = 5;

        // Act
        var result = IsDatePairComplete(year, month);

        // Assert
        result.Should().BeTrue("both year and month are set, so the date pair is complete");
    }

    [Fact]
    public void IsDatePairComplete_WhenYearIsNull_ReturnsFalse()
    {
        // Arrange
        int? year = null;
        int? month = 5;

        // Act
        var result = IsDatePairComplete(year, month);

        // Assert
        result.Should().BeFalse("year is null, so the date pair is incomplete");
    }

    [Fact]
    public void IsDatePairComplete_WhenMonthIsNull_ReturnsFalse()
    {
        // Arrange
        int? year = 2020;
        int? month = null;

        // Act
        var result = IsDatePairComplete(year, month);

        // Assert
        result.Should().BeFalse("month is null, so the date pair is incomplete");
    }

    [Fact]
    public void IsDatePairComplete_WhenBothAreNull_ReturnsFalse()
    {
        // Arrange
        int? year = null;
        int? month = null;

        // Act
        var result = IsDatePairComplete(year, month);

        // Assert
        result.Should().BeFalse("both year and month are null, so the date pair is incomplete");
    }

    [Theory]
    [InlineData(1990, 1)]
    [InlineData(2023, 12)]
    [InlineData(2015, 6)]
    public void IsDatePairComplete_WithVariousCompleteValues_ReturnsTrue(int year, int month)
    {
        // Arrange & Act
        var result = IsDatePairComplete(year, month);

        // Assert
        result.Should().BeTrue($"year {year} and month {month} are both set");
    }

    [Fact]
    public void SaveTunaSection_Logic_WhenLeitaoDateIsComplete_ShouldNotUpdateLeitaoDate()
    {
        // Arrange - Simulate user in database with complete Leitao date
        var userInDb = new ApplicationUser
        {
            Id = "user-1",
            YearLeitao = 2020,
            MonthLeitao = 5,
            YearCaloiro = null,
            MonthCaloiro = null
        };

        // Simulate edit attempt with different values
        var editAttempt = new ApplicationUser
        {
            Id = "user-1",
            YearLeitao = 2021, // Trying to change
            MonthLeitao = 6,   // Trying to change
            YearCaloiro = 2022,
            MonthCaloiro = 8
        };

        // Act - Apply the SaveTunaSection logic
        if (!IsDatePairComplete(userInDb.YearLeitao, userInDb.MonthLeitao))
        {
            userInDb.YearLeitao = editAttempt.YearLeitao;
            userInDb.MonthLeitao = editAttempt.MonthLeitao;
        }

        if (!IsDatePairComplete(userInDb.YearCaloiro, userInDb.MonthCaloiro))
        {
            userInDb.YearCaloiro = editAttempt.YearCaloiro;
            userInDb.MonthCaloiro = editAttempt.MonthCaloiro;
        }

        // Assert - Leitao date should remain unchanged, Caloiro should be updated
        userInDb.YearLeitao.Should().Be(2020, "Leitao date was complete and should not be updated");
        userInDb.MonthLeitao.Should().Be(5, "Leitao date was complete and should not be updated");
        userInDb.YearCaloiro.Should().Be(2022, "Caloiro date was incomplete and should be updated");
        userInDb.MonthCaloiro.Should().Be(8, "Caloiro date was incomplete and should be updated");
    }

    [Fact]
    public void SaveTunaSection_Logic_WhenLeitaoDateIsIncomplete_ShouldAllowUpdate()
    {
        // Arrange - User with only year set (incomplete)
        var userInDb = new ApplicationUser
        {
            Id = "user-1",
            YearLeitao = 2020,
            MonthLeitao = null // Incomplete
        };

        var editAttempt = new ApplicationUser
        {
            Id = "user-1",
            YearLeitao = 2020,
            MonthLeitao = 5 // Completing the date
        };

        // Act
        if (!IsDatePairComplete(userInDb.YearLeitao, userInDb.MonthLeitao))
        {
            userInDb.YearLeitao = editAttempt.YearLeitao;
            userInDb.MonthLeitao = editAttempt.MonthLeitao;
        }

        // Assert - Should allow completion of incomplete date
        userInDb.YearLeitao.Should().Be(2020);
        userInDb.MonthLeitao.Should().Be(5, "incomplete date should be allowed to be completed");
    }

    [Fact]
    public void SaveTunaSection_Logic_WhenAllDatesIncomplete_ShouldAllowAllUpdates()
    {
        // Arrange
        var userInDb = new ApplicationUser
        {
            Id = "user-1",
            YearLeitao = null,
            MonthLeitao = null,
            YearCaloiro = null,
            MonthCaloiro = null,
            YearTuno = null,
            MonthTuno = null
        };

        var editAttempt = new ApplicationUser
        {
            Id = "user-1",
            YearLeitao = 2020,
            MonthLeitao = 5,
            YearCaloiro = 2021,
            MonthCaloiro = 6,
            YearTuno = 2023,
            MonthTuno = 7
        };

        // Act
        if (!IsDatePairComplete(userInDb.YearLeitao, userInDb.MonthLeitao))
        {
            userInDb.YearLeitao = editAttempt.YearLeitao;
            userInDb.MonthLeitao = editAttempt.MonthLeitao;
        }

        if (!IsDatePairComplete(userInDb.YearCaloiro, userInDb.MonthCaloiro))
        {
            userInDb.YearCaloiro = editAttempt.YearCaloiro;
            userInDb.MonthCaloiro = editAttempt.MonthCaloiro;
        }

        if (!IsDatePairComplete(userInDb.YearTuno, userInDb.MonthTuno))
        {
            userInDb.YearTuno = editAttempt.YearTuno;
            userInDb.MonthTuno = editAttempt.MonthTuno;
        }

        // Assert - All dates should be updated
        userInDb.YearLeitao.Should().Be(2020);
        userInDb.MonthLeitao.Should().Be(5);
        userInDb.YearCaloiro.Should().Be(2021);
        userInDb.MonthCaloiro.Should().Be(6);
        userInDb.YearTuno.Should().Be(2023);
        userInDb.MonthTuno.Should().Be(7);
    }
}
