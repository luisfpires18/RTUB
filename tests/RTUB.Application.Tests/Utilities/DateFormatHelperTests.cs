using FluentAssertions;
using RTUB.Application.Utilities;

namespace RTUB.Application.Tests.Utilities;

/// <summary>
/// Tests for DateFormatHelper utility
/// </summary>
public class DateFormatHelperTests
{
    [Fact]
    public void FormatEventDateRange_SingleDay_FormatsCorrectly()
    {
        // Arrange
        var startDate = new DateTime(2025, 11, 20, 14, 30, 0);

        // Act
        var result = DateFormatHelper.FormatEventDateRange(startDate);

        // Assert
        result.Should().Be("20 de novembro de 2025");
    }

    [Fact]
    public void FormatEventDateRange_SameMonthYear_FormatsCorrectly()
    {
        // Arrange
        var startDate = new DateTime(2025, 11, 20);
        var endDate = new DateTime(2025, 11, 22);

        // Act
        var result = DateFormatHelper.FormatEventDateRange(startDate, endDate);

        // Assert
        result.Should().Be("20–22 de novembro de 2025");
    }

    [Fact]
    public void FormatEventDateRange_DifferentMonths_SameYear_FormatsCorrectly()
    {
        // Arrange
        var startDate = new DateTime(2025, 11, 20);
        var endDate = new DateTime(2025, 12, 2);

        // Act
        var result = DateFormatHelper.FormatEventDateRange(startDate, endDate);

        // Assert
        result.Should().Be("20 de novembro – 02 de dezembro de 2025");
    }

    [Fact]
    public void FormatEventDateRange_DifferentYears_FormatsCorrectly()
    {
        // Arrange
        var startDate = new DateTime(2025, 12, 28);
        var endDate = new DateTime(2026, 1, 2);

        // Act
        var result = DateFormatHelper.FormatEventDateRange(startDate, endDate);

        // Assert
        result.Should().Be("28 de dezembro de 2025 – 02 de janeiro de 2026");
    }

    [Fact]
    public void FormatEventDateRange_SameStartAndEnd_FormatsCorrectly()
    {
        // Arrange
        var startDate = new DateTime(2025, 11, 20);
        var endDate = new DateTime(2025, 11, 20);

        // Act
        var result = DateFormatHelper.FormatEventDateRange(startDate, endDate);

        // Assert
        result.Should().Be("20 de novembro de 2025");
    }

    [Fact]
    public void FormatEventDateRangeForEmail_SingleDay_FormatsCorrectly()
    {
        // Arrange
        var startDate = new DateTime(2025, 11, 20);

        // Act
        var result = DateFormatHelper.FormatEventDateRangeForEmail(startDate);

        // Assert
        result.Should().Be("quinta-feira, 20 de novembro de 2025");
    }

    [Fact]
    public void FormatEventDateRangeForEmail_SameMonthYear_FormatsCorrectly()
    {
        // Arrange
        var startDate = new DateTime(2025, 11, 20);
        var endDate = new DateTime(2025, 11, 22);

        // Act
        var result = DateFormatHelper.FormatEventDateRangeForEmail(startDate, endDate);

        // Assert
        result.Should().Be("20 - 22 de novembro de 2025");
    }

    [Fact]
    public void FormatEventDateRangeForEmail_DifferentMonths_SameYear_FormatsCorrectly()
    {
        // Arrange
        var startDate = new DateTime(2025, 11, 20);
        var endDate = new DateTime(2025, 12, 2);

        // Act
        var result = DateFormatHelper.FormatEventDateRangeForEmail(startDate, endDate);

        // Assert
        result.Should().Be("20 de novembro - 02 de dezembro de 2025");
    }

    [Fact]
    public void FormatEventDateRangeForEmail_DifferentYears_FormatsCorrectly()
    {
        // Arrange
        var startDate = new DateTime(2025, 12, 28);
        var endDate = new DateTime(2026, 1, 2);

        // Act
        var result = DateFormatHelper.FormatEventDateRangeForEmail(startDate, endDate);

        // Assert
        result.Should().Be("28 de dezembro de 2025 - 02 de janeiro de 2026");
    }
}
