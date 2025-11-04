using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Core.Tests.Entities;

public class FiscalYearTests
{
    [Fact]
    public void Create_WithValidYears_CreatesFiscalYear()
    {
        // Arrange
        var startYear = 2023;
        var endYear = 2024;

        // Act
        var fiscalYear = FiscalYear.Create(startYear, endYear);

        // Assert
        fiscalYear.Should().NotBeNull();
        fiscalYear.StartYear.Should().Be(startYear);
        fiscalYear.EndYear.Should().Be(endYear);
    }

    [Theory]
    [InlineData(2020, 2021)]
    [InlineData(2022, 2023)]
    [InlineData(2024, 2025)]
    public void Create_WithDifferentValidYears_CreatesFiscalYear(int startYear, int endYear)
    {
        // Act
        var fiscalYear = FiscalYear.Create(startYear, endYear);

        // Assert
        fiscalYear.StartYear.Should().Be(startYear);
        fiscalYear.EndYear.Should().Be(endYear);
    }

    [Fact]
    public void Create_WithSameYears_ThrowsArgumentException()
    {
        // Arrange
        var year = 2023;

        // Act
        var act = () => FiscalYear.Create(year, year);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*End year must be after start year*");
    }

    [Fact]
    public void Create_WithEndYearBeforeStartYear_ThrowsArgumentException()
    {
        // Act
        var act = () => FiscalYear.Create(2024, 2023);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*End year must be after start year*");
    }

    [Fact]
    public void GetFiscalYearString_ReturnsCorrectFormat()
    {
        // Arrange
        var fiscalYear = FiscalYear.Create(2023, 2024);

        // Act
        var result = fiscalYear.GetFiscalYearString();

        // Assert
        result.Should().Be("2023-2024");
    }

    [Fact]
    public void FiscalYearString_Property_ReturnsCorrectFormat()
    {
        // Arrange
        var fiscalYear = FiscalYear.Create(2023, 2024);

        // Act
        var result = fiscalYear.FiscalYearString;

        // Assert
        result.Should().Be("2023-2024");
    }

    [Theory]
    [InlineData(2020, 2021, "2020-2021")]
    [InlineData(2021, 2022, "2021-2022")]
    [InlineData(2024, 2025, "2024-2025")]
    public void GetFiscalYearString_WithDifferentYears_ReturnsCorrectFormat(int start, int end, string expected)
    {
        // Arrange
        var fiscalYear = FiscalYear.Create(start, end);

        // Act
        var result = fiscalYear.GetFiscalYearString();

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void FiscalYear_Properties_CanBeSet()
    {
        // Arrange & Act
        var fiscalYear = new FiscalYear
        {
            StartYear = 2023,
            EndYear = 2024
        };

        // Assert
        fiscalYear.StartYear.Should().Be(2023);
        fiscalYear.EndYear.Should().Be(2024);
    }

    [Fact]
    public void FiscalYear_InheritsFromBaseEntity()
    {
        // Arrange & Act
        var fiscalYear = FiscalYear.Create(2023, 2024);

        // Assert
        fiscalYear.Should().BeAssignableTo<BaseEntity>();
    }
}
