using FluentAssertions;
using RTUB.Core.Enums;

namespace RTUB.Core.Tests.Enums;

/// <summary>
/// Unit tests for InstrumentType enum
/// </summary>
public class InstrumentTypeTests
{
    [Fact]
    public void InstrumentType_AllValuesAreDefined()
    {
        // Arrange
        var expectedValues = new[]
        {
            InstrumentType.Guitarra,
            InstrumentType.Bandolim,
            InstrumentType.Cavaquinho,
            InstrumentType.Acordeao,
            InstrumentType.Fagote,
            InstrumentType.Flauta,
            InstrumentType.Baixo,
            InstrumentType.Percussao,
            InstrumentType.Pandeireta,
            InstrumentType.Estandarte,
        };

        // Act
        var actualValues = Enum.GetValues<InstrumentType>();

        // Assert
        actualValues.Should().BeEquivalentTo(expectedValues);
    }

    [Theory]
    [InlineData("Guitarra", InstrumentType.Guitarra)]
    [InlineData("Bandolim", InstrumentType.Bandolim)]
    [InlineData("Cavaquinho", InstrumentType.Cavaquinho)]
    [InlineData("Acordeao", InstrumentType.Acordeao)]
    [InlineData("Percussao", InstrumentType.Percussao)]
    public void InstrumentType_CanParse(string value, InstrumentType expected)
    {
        // Act
        var result = Enum.Parse<InstrumentType>(value);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void InstrumentType_Count_IsEleven()
    {
        // Act
        var count = Enum.GetValues<InstrumentType>().Length;

        // Assert
        count.Should().Be(10);
    }
}
