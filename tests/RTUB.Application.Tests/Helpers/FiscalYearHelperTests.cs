using FluentAssertions;
using RTUB.Application.Helpers;

namespace RTUB.Application.Tests.Helpers;

/// <summary>
/// Unit tests for FiscalYearHelper
/// Tests fiscal year calculations (Sept-Aug cycle)
/// </summary>
public class FiscalYearHelperTests
{
    [Theory]
    [InlineData(2024, 9, 2024)]  // September 2024 -> FY 2024-2025
    [InlineData(2024, 10, 2024)] // October 2024 -> FY 2024-2025
    [InlineData(2024, 11, 2024)] // November 2024 -> FY 2024-2025
    [InlineData(2024, 12, 2024)] // December 2024 -> FY 2024-2025
    [InlineData(2025, 1, 2024)]  // January 2025 -> FY 2024-2025
    [InlineData(2025, 2, 2024)]  // February 2025 -> FY 2024-2025
    [InlineData(2025, 3, 2024)]  // March 2025 -> FY 2024-2025
    [InlineData(2025, 4, 2024)]  // April 2025 -> FY 2024-2025
    [InlineData(2025, 5, 2024)]  // May 2025 -> FY 2024-2025
    [InlineData(2025, 6, 2024)]  // June 2025 -> FY 2024-2025
    [InlineData(2025, 7, 2024)]  // July 2025 -> FY 2024-2025
    [InlineData(2025, 8, 2024)]  // August 2025 -> FY 2024-2025
    public void GetFiscalYearStartYear_WithDate_ReturnsCorrectStartYear(int year, int month, int expectedStartYear)
    {
        // Arrange
        var date = new DateTime(year, month, 15);

        // Act
        var result = FiscalYearHelper.GetFiscalYearStartYear(date);

        // Assert
        result.Should().Be(expectedStartYear);
    }

    [Fact]
    public void GetFiscalYearStartYear_SeptemberFirst_ReturnsCurrentYear()
    {
        // Arrange
        var date = new DateTime(2024, 9, 1);

        // Act
        var result = FiscalYearHelper.GetFiscalYearStartYear(date);

        // Assert
        result.Should().Be(2024);
    }

    [Fact]
    public void GetFiscalYearStartYear_AugustLast_ReturnsPreviousYear()
    {
        // Arrange
        var date = new DateTime(2024, 8, 31);

        // Act
        var result = FiscalYearHelper.GetFiscalYearStartYear(date);

        // Assert
        result.Should().Be(2023);
    }

    [Fact]
    public void GetCurrentFiscalYearStartYear_ReturnsValidYear()
    {
        // Act
        var result = FiscalYearHelper.GetCurrentFiscalYearStartYear();

        // Assert
        var today = DateTime.Today;
        var expectedYear = today.Month >= 9 ? today.Year : today.Year - 1;
        result.Should().Be(expectedYear);
    }

    [Fact]
    public void GetCurrentFiscalYearString_ReturnsValidFormat()
    {
        // Act
        var result = FiscalYearHelper.GetCurrentFiscalYearString();

        // Assert
        result.Should().MatchRegex(@"^\d{4}-\d{4}$");

        var parts = result.Split('-');
        var startYear = int.Parse(parts[0]);
        var endYear = int.Parse(parts[1]);
        endYear.Should().Be(startYear + 1);
    }

    [Fact]
    public void GetCurrentFiscalYearString_MatchesStartYear()
    {
        // Act
        var yearString = FiscalYearHelper.GetCurrentFiscalYearString();
        var startYear = FiscalYearHelper.GetCurrentFiscalYearStartYear();

        // Assert
        yearString.Should().StartWith(startYear.ToString());
    }

    [Theory]
    [InlineData(2020, 9, "2020-2021")]
    [InlineData(2021, 1, "2020-2021")]
    [InlineData(2023, 12, "2023-2024")]
    [InlineData(2024, 8, "2023-2024")]
    public void GetFiscalYearString_ForDate_ReturnsCorrectFormat(int year, int month, string expected)
    {
        // Arrange
        var date = new DateTime(year, month, 15);
        var startYear = FiscalYearHelper.GetFiscalYearStartYear(date);

        // Act
        var result = $"{startYear}-{startYear + 1}";

        // Assert
        result.Should().Be(expected);
    }
}
