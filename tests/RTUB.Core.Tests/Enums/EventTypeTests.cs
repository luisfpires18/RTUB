using FluentAssertions;
using RTUB.Core.Enums;

namespace RTUB.Core.Tests.Enums;

/// <summary>
/// Unit tests for EventType enum
/// Tests enum values and conversions
/// </summary>
public class EventTypeTests
{
    [Fact]
    public void EventType_AllValuesAreDefined()
    {
        // Arrange
        var expectedValues = new[]
        {
            EventType.Festival,
            EventType.Atuacao,
            EventType.Casamento,
            EventType.Serenata,
            EventType.Arraial,
            EventType.Convivio,
            EventType.Nerba,
            EventType.Missa,
            EventType.Batizado
        };

        // Act
        var actualValues = Enum.GetValues<EventType>();

        // Assert
        actualValues.Should().BeEquivalentTo(expectedValues);
    }

    [Theory]
    [InlineData("Festival", EventType.Festival)]    
    [InlineData("Atuacao", EventType.Atuacao)]
    [InlineData("Casamento", EventType.Casamento)]
    [InlineData("Serenata", EventType.Serenata)]
    [InlineData("Arraial", EventType.Arraial)]
    [InlineData("Convivio", EventType.Convivio)]
    [InlineData("Nerba", EventType.Nerba)]
    [InlineData("Missa", EventType.Missa)]
    [InlineData("Batizado", EventType.Batizado)]
    public void EventType_CanParse(string value, EventType expected)
    {
        // Act
        var result = Enum.Parse<EventType>(value);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void EventType_CanConvertToString()
    {
        // Arrange
        var eventType = EventType.Festival;

        // Act
        var result = eventType.ToString();

        // Assert
        result.Should().Be("Festival");
    }

    [Fact]
    public void EventType_Count_IsNine()
    {
        // Act
        var count = Enum.GetValues<EventType>().Length;

        // Assert
        count.Should().Be(9);
    }
}
