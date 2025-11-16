using Xunit;
using FluentAssertions;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Core.Tests.Helpers;

/// <summary>
/// Unit tests for PositionHelper
/// </summary>
public class PositionHelperTests
{
    #region GetDisplayName Tests

    [Theory]
    [InlineData(Position.Magister, "Magister")]
    [InlineData(Position.ViceMagister, "Vice-Magister")]
    [InlineData(Position.Secretario, "Secretário")]
    [InlineData(Position.PrimeiroTesoureiro, "Primeiro Tesoureiro")]
    [InlineData(Position.SegundoTesoureiro, "Segundo Tesoureiro")]
    [InlineData(Position.PresidenteMesaAssembleia, "Presidente da Mesa de Assembleia")]
    [InlineData(Position.PrimeiroSecretarioMesaAssembleia, "Primeiro Secretário da Mesa de Assembleia")]
    [InlineData(Position.SegundoSecretarioMesaAssembleia, "Segundo Secretário da Mesa de Assembleia")]
    [InlineData(Position.PresidenteConselhoFiscal, "Presidente do Conselho Fiscal")]
    [InlineData(Position.PrimeiroRelatorConselhoFiscal, "Primeiro Relator do Conselho Fiscal")]
    [InlineData(Position.SegundoRelatorConselhoFiscal, "Segundo Relator do Conselho Fiscal")]
    [InlineData(Position.PresidenteConselhoVeteranos, "Presidente do Conselho de Veteranos")]
    [InlineData(Position.Ensaiador, "Ensaiador")]
    public void GetDisplayName_ReturnsCorrectPortugueseName(Position position, string expectedName)
    {
        // Act
        var displayName = PositionHelper.GetDisplayName(position);

        // Assert
        displayName.Should().Be(expectedName, $"Position {position} should display as {expectedName}");
    }

    [Fact]
    public void GetDisplayName_Magister_ReturnsMagister()
    {
        // Arrange
        var position = Position.Magister;

        // Act
        var displayName = PositionHelper.GetDisplayName(position);

        // Assert
        displayName.Should().Be("Magister");
    }

    [Fact]
    public void GetDisplayName_PresidenteMesaAssembleia_ReturnsFullTitle()
    {
        // Arrange
        var position = Position.PresidenteMesaAssembleia;

        // Act
        var displayName = PositionHelper.GetDisplayName(position);

        // Assert
        displayName.Should().Be("Presidente da Mesa de Assembleia");
    }

    [Fact]
    public void GetDisplayName_PresidenteConselhoVeteranos_ReturnsFullTitle()
    {
        // Arrange
        var position = Position.PresidenteConselhoVeteranos;

        // Act
        var displayName = PositionHelper.GetDisplayName(position);

        // Assert
        displayName.Should().Be("Presidente do Conselho de Veteranos");
    }

    #endregion
}
