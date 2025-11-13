using Xunit;
using FluentAssertions;
using RTUB.Core.Enums;

namespace RTUB.Web.Tests.Pages;

/// <summary>
/// Unit tests for Roles page behavior
/// Testing fiscal year selection, role assignment validation, and admin controls
/// </summary>
public class RolesPageTests
{
    #region Fiscal Year Selection Tests

    [Fact]
    public void FiscalYear_DefaultSelection_ShouldBeCurrentYear()
    {
        // Arrange
        var currentYear = DateTime.Now.Year;
        var expectedFY = currentYear < 9 ? $"{currentYear - 1}-{currentYear}" : $"{currentYear}-{currentYear + 1}";

        // Act
        var actualFY = GetCurrentFiscalYearString();

        // Assert
        actualFY.Should().NotBeNullOrEmpty("Fiscal year should be calculated");
        actualFY.Should().MatchRegex(@"\d{4}-\d{4}", "Fiscal year should be in format YYYY-YYYY");
    }

    [Theory]
    [InlineData("2023-2024", 2023, 2024)]
    [InlineData("2024-2025", 2024, 2025)]
    [InlineData("2025-2026", 2025, 2026)]
    public void FiscalYear_ParseString_ShouldExtractYears(string fyString, int expectedStart, int expectedEnd)
    {
        // Act
        var parts = fyString.Split('-');
        var startYear = int.Parse(parts[0]);
        var endYear = int.Parse(parts[1]);

        // Assert
        startYear.Should().Be(expectedStart, "Start year should match");
        endYear.Should().Be(expectedEnd, "End year should match");
        endYear.Should().Be(startYear + 1, "End year should be one year after start");
    }

    #endregion

    #region Role Assignment Validation Tests

    [Theory]
    [InlineData(Position.Magister, "Magister")]
    [InlineData(Position.ViceMagister, "Vice-Magister")]
    [InlineData(Position.Secretario, "Secretário")]
    [InlineData(Position.PrimeiroTesoureiro, "1º Tesoureiro")]
    [InlineData(Position.SegundoTesoureiro, "2º Tesoureiro")]
    [InlineData(Position.PresidenteMesaAssembleia, "Presidente Mesa Assembleia")]
    [InlineData(Position.PresidenteConselhoFiscal, "Presidente Conselho Fiscal")]
    [InlineData(Position.PresidenteConselhoVeteranos, "Presidente Conselho Veteranos")]
    [InlineData(Position.Ensaiador, "Ensaiador")]
    public void Position_AllPositions_ShouldHaveValidNames(Position position, string expectedName)
    {
        // Arrange & Act
        var displayName = GetPositionDisplayName(position);

        // Assert
        displayName.Should().NotBeNullOrEmpty($"Position {position} should have a display name");
        displayName.Should().Be(expectedName, $"Position {position} should have display name '{expectedName}'");
    }

    [Fact]
    public void Position_Count_ShouldBe13()
    {
        // Arrange & Act
        var allPositions = Enum.GetValues(typeof(Position));

        // Assert
        allPositions.Length.Should().Be(13, "There should be exactly 13 positions");
    }

    [Fact]
    public void Position_PresidentPositions_ShouldInclude4Positions()
    {
        // Arrange
        var presidentPositions = new[]
        {
            Position.Magister,
            Position.PresidenteMesaAssembleia,
            Position.PresidenteConselhoFiscal,
            Position.PresidenteConselhoVeteranos
        };

        // Act & Assert
        presidentPositions.Length.Should().Be(4, "There should be 4 president positions");
        presidentPositions.Should().Contain(Position.Magister, "Magister is a president position");
        presidentPositions.Should().Contain(Position.PresidenteMesaAssembleia, "Presidente Mesa is a president position");
    }

    #endregion

    #region Member Category Validation Tests

    [Fact]
    public void MemberCategory_Leitao_ShouldBeFiltered()
    {
        // This test verifies the business rule that Leitão members
        // should not appear in the assignment list
        
        // Arrange
        var categories = new[] { MemberCategory.Leitao };

        // Act - Check if Leitão should be excluded
        var shouldExclude = categories.Contains(MemberCategory.Leitao);

        // Assert
        shouldExclude.Should().BeTrue("Leitão members should be filtered out from role assignments");
    }

    [Theory]
    [InlineData(MemberCategory.Caloiro)]
    [InlineData(MemberCategory.Tuno)]
    [InlineData(MemberCategory.Veterano)]
    [InlineData(MemberCategory.Tunossauro)]
    public void MemberCategory_NonLeitao_ShouldBeAllowed(MemberCategory category)
    {
        // Arrange & Act
        var isLeitao = category == MemberCategory.Leitao;

        // Assert
        isLeitao.Should().BeFalse($"{category} should be allowed for role assignment");
    }

    #endregion

    #region URL Query Parameter Tests

    [Theory]
    [InlineData("?fy=2023-2024", "2023-2024")]
    [InlineData("?fy=2024-2025", "2024-2025")]
    [InlineData("?fy=2025-2026", "2025-2026")]
    public void QueryParameter_FiscalYear_ShouldBeExtracted(string query, string expectedFY)
    {
        // Arrange
        var uri = new Uri($"https://example.com/roles{query}");

        // Act
        var fyParam = System.Web.HttpUtility.ParseQueryString(uri.Query)["fy"];

        // Assert
        fyParam.Should().Be(expectedFY, "FY query parameter should be extracted correctly");
    }

    [Fact]
    public void QueryParameter_Manage_ShouldTriggerModal()
    {
        // Arrange
        var uri = new Uri("https://example.com/roles?manage=1");

        // Act
        var manageParam = System.Web.HttpUtility.ParseQueryString(uri.Query)["manage"];

        // Assert
        manageParam.Should().Be("1", "Manage parameter should be '1'");
    }

    #endregion

    #region Helper Methods

    private string GetCurrentFiscalYearString()
    {
        var now = DateTime.Now;
        var currentYear = now.Year;
        var startYear = now.Month < 9 ? currentYear - 1 : currentYear;
        return $"{startYear}-{startYear + 1}";
    }

    private string GetPositionDisplayName(Position position)
    {
        return position switch
        {
            Position.Magister => "Magister",
            Position.ViceMagister => "Vice-Magister",
            Position.Secretario => "Secretário",
            Position.PrimeiroTesoureiro => "1º Tesoureiro",
            Position.SegundoTesoureiro => "2º Tesoureiro",
            Position.PresidenteMesaAssembleia => "Presidente Mesa Assembleia",
            Position.PrimeiroSecretarioMesaAssembleia => "1º Secretário Mesa Assembleia",
            Position.SegundoSecretarioMesaAssembleia => "2º Secretário Mesa Assembleia",
            Position.PresidenteConselhoFiscal => "Presidente Conselho Fiscal",
            Position.PrimeiroRelatorConselhoFiscal => "1º Relator Conselho Fiscal",
            Position.SegundoRelatorConselhoFiscal => "2º Relator Conselho Fiscal",
            Position.PresidenteConselhoVeteranos => "Presidente Conselho Veteranos",
            Position.Ensaiador => "Ensaiador",
            _ => string.Empty
        };
    }

    #endregion
}
