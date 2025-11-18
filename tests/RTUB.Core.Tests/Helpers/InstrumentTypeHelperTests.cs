using FluentAssertions;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Core.Tests.Helpers;

/// <summary>
/// Unit tests for InstrumentTypeHelper
/// Tests instrument type display and parsing logic
/// </summary>
public class InstrumentTypeHelperTests
{
    [Theory]
    [InlineData(InstrumentType.Guitarra, "Guitarra")]
    [InlineData(InstrumentType.Bandolim, "Bandolim")]
    [InlineData(InstrumentType.Cavaquinho, "Cavaquinho")]
    [InlineData(InstrumentType.Acordeao, "Acordeão")]
    [InlineData(InstrumentType.Fagote, "Fagote")]
    [InlineData(InstrumentType.Flauta, "Flauta")]
    [InlineData(InstrumentType.Baixo, "Baixo")]
    [InlineData(InstrumentType.Contrabaixo, "Contrabaixo")]
    [InlineData(InstrumentType.Percussao, "Percussão")]
    [InlineData(InstrumentType.Pandeireta, "Pandeireta")]
    [InlineData(InstrumentType.Estandarte, "Estandarte")]
    [InlineData(InstrumentType.Violino, "Violino")]
    public void GetDisplayName_ReturnsCorrectName(InstrumentType instrument, string expected)
    {
        // Act
        var result = InstrumentTypeHelper.GetDisplayName(instrument);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Guitarra", InstrumentType.Guitarra)]
    [InlineData("Bandolim", InstrumentType.Bandolim)]
    [InlineData("Cavaquinho", InstrumentType.Cavaquinho)]
    [InlineData("Acordeão", InstrumentType.Acordeao)]
    [InlineData("Percussão", InstrumentType.Percussao)]
    [InlineData("Fagote", InstrumentType.Fagote)]
    [InlineData("Flauta", InstrumentType.Flauta)]
    [InlineData("Baixo", InstrumentType.Baixo)]
    [InlineData("Contrabaixo", InstrumentType.Contrabaixo)]
    [InlineData("Pandeireta", InstrumentType.Pandeireta)]
    [InlineData("Estandarte", InstrumentType.Estandarte)]
    [InlineData("Violino", InstrumentType.Violino)]
    public void ParseDisplayName_WithValidName_ReturnsCorrectEnum(string displayName, InstrumentType expected)
    {
        // Act
        var result = InstrumentTypeHelper.ParseDisplayName(displayName);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("guitarra")] // lowercase
    [InlineData("GUITARRA")] // uppercase
    [InlineData("GuItArRa")] // mixed case
    public void ParseDisplayName_IsCaseInsensitive(string displayName)
    {
        // Act
        var result = InstrumentTypeHelper.ParseDisplayName(displayName);

        // Assert
        result.Should().Be(InstrumentType.Guitarra);
    }

    [Theory]
    [InlineData("InvalidInstrument")]
    [InlineData("Piano")]
    [InlineData("")]
    public void ParseDisplayName_WithInvalidName_ReturnsNull(string displayName)
    {
        // Act
        var result = InstrumentTypeHelper.ParseDisplayName(displayName);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ParseDisplayName_WithNull_ReturnsNull()
    {
        // Act
        var result = InstrumentTypeHelper.ParseDisplayName(null!);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ParseDisplayName_WithWhitespace_ReturnsNull()
    {
        // Act
        var result = InstrumentTypeHelper.ParseDisplayName("   ");

        // Assert
        result.Should().BeNull();
    }
}
