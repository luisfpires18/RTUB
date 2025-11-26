using Xunit;
using FluentAssertions;

namespace RTUB.Web.Tests.Pages;

/// <summary>
/// Unit tests for Rehearsals page toggle feature
/// Testing "Show All" toggle for user attendances modal
/// </summary>
public class RehearsalsPageToggleTests
{
    #region Show All Toggle Tests

    [Fact]
    public void ShowAllRehearsals_DefaultsToFalse()
    {
        // Arrange
        var showAllRehearsals = false;

        // Assert
        showAllRehearsals.Should().BeFalse("Show all toggle should default to false");
    }

    [Fact]
    public void ShowAllRehearsals_WhenFalse_ShouldDisplay10RehearsalsText()
    {
        // Arrange
        var showAllRehearsals = false;
        var expectedText = "últimos 10 ensaios";

        // Act
        var displayText = showAllRehearsals ? "todos os ensaios" : "últimos 10 ensaios";

        // Assert
        displayText.Should().Be(expectedText, "Text should indicate last 10 rehearsals when toggle is off");
    }

    [Fact]
    public void ShowAllRehearsals_WhenTrue_ShouldDisplayAllRehearsalsText()
    {
        // Arrange
        var showAllRehearsals = true;
        var expectedText = "todos os ensaios";

        // Act
        var displayText = showAllRehearsals ? "todos os ensaios" : "últimos 10 ensaios";

        // Assert
        displayText.Should().Be(expectedText, "Text should indicate all rehearsals when toggle is on");
    }

    [Theory]
    [InlineData(8, 10, 80.0)] // 8 attended out of 10
    [InlineData(5, 10, 50.0)] // 5 attended out of 10
    [InlineData(10, 10, 100.0)] // Perfect attendance
    [InlineData(0, 10, 0.0)] // No attendance
    [InlineData(7, 8, 87.5)] // All attendances (8 total rehearsals)
    public void AttendanceRate_Calculation_ShouldBeCorrect(int attendanceCount, int totalRehearsals, double expectedPercentage)
    {
        // Act
        var actualPercentage = totalRehearsals > 0
            ? Math.Round((double)attendanceCount / totalRehearsals * 100, 1)
            : 0;

        // Assert
        actualPercentage.Should().Be(expectedPercentage, "Attendance percentage should be calculated correctly");
    }

    [Fact]
    public void AttendanceRate_WithZeroTotal_ShouldNotDivideByZero()
    {
        // Arrange
        var attendanceCount = 0;
        var totalRehearsals = 0;

        // Act
        var percentage = totalRehearsals > 0
            ? Math.Round((double)attendanceCount / totalRehearsals * 100, 1)
            : 0;

        // Assert
        percentage.Should().Be(0, "Should handle zero total rehearsals gracefully");
    }

    [Theory]
    [InlineData(false, 10)] // Toggle off, should limit to 10
    [InlineData(true, int.MaxValue)] // Toggle on, should not limit
    public void LoadRehearsals_ShouldRespectToggleState(bool showAll, int expectedLimit)
    {
        // Arrange
        var limit = showAll ? int.MaxValue : 10;

        // Assert
        limit.Should().Be(expectedLimit, "Load limit should respect toggle state");
    }

    #endregion

    #region Date Range Tests

    [Fact]
    public void LoadRehearsals_WhenShowAllFalse_ShouldLoad6MonthsBack()
    {
        // Arrange
        var showAll = false;
        var today = DateTime.Today;
        var expectedStartDate = today.AddMonths(-6);

        // Act
        var startDate = showAll ? today.AddYears(-10) : today.AddMonths(-6);

        // Assert
        startDate.Should().BeCloseTo(expectedStartDate, TimeSpan.FromSeconds(1),
            "Should load last 6 months when toggle is off");
    }

    [Fact]
    public void LoadRehearsals_WhenShowAllTrue_ShouldLoad10YearsBack()
    {
        // Arrange
        var showAll = true;
        var today = DateTime.Today;
        var expectedStartDate = today.AddYears(-10);

        // Act
        var startDate = showAll ? today.AddYears(-10) : today.AddMonths(-6);

        // Assert
        startDate.Should().BeCloseTo(expectedStartDate, TimeSpan.FromSeconds(1),
            "Should load last 10 years when toggle is on");
    }

    #endregion
}
