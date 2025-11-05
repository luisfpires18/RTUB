using FluentAssertions;
using RTUB.Core.Enums;

namespace RTUB.Core.Tests.Enums;

/// <summary>
/// Unit tests for Position enum
/// </summary>
public class PositionTests
{
    [Fact]
    public void Position_AllValuesAreDefined()
    {
        // Arrange
        var expectedValues = new[]
        {
            Position.Magister,
            Position.ViceMagister,
            Position.Secretario,
            Position.PrimeiroTesoureiro,
            Position.SegundoTesoureiro,
            Position.PresidenteMesaAssembleia,
            Position.PrimeiroSecretarioMesaAssembleia,
            Position.SegundoSecretarioMesaAssembleia,
            Position.PresidenteConselhoFiscal,
            Position.PrimeiroRelatorConselhoFiscal,
            Position.SegundoRelatorConselhoFiscal,
            Position.PresidenteConselhoVeteranos,
            Position.Ensaiador
        };

        // Act
        var actualValues = Enum.GetValues<Position>();

        // Assert
        actualValues.Should().BeEquivalentTo(expectedValues);
    }

    [Theory]
    [InlineData("Magister", Position.Magister)]
    [InlineData("ViceMagister", Position.ViceMagister)]
    [InlineData("Secretario", Position.Secretario)]
    [InlineData("PrimeiroTesoureiro", Position.PrimeiroTesoureiro)]
    [InlineData("SegundoTesoureiro", Position.SegundoTesoureiro)]
    public void Position_CanParse(string value, Position expected)
    {
        // Act
        var result = Enum.Parse<Position>(value);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Position_Count_IsTwelve()
    {
        // Act
        var count = Enum.GetValues<Position>().Length;

        // Assert
        count.Should().Be(13);
    }
}
