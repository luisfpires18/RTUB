using FluentAssertions;
using RTUB.Application.Extensions;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Extensions;

/// <summary>
/// Unit tests for EventTypeExtensions
/// Tests display name mapping for EventType enum values
/// </summary>
public class EventTypeExtensionsTests
{
    [Theory]
    [InlineData(EventType.Atuacao, "Atuação")]
    [InlineData(EventType.Festival, "Festival")]
    [InlineData(EventType.Casamento, "Casamento")]
    [InlineData(EventType.Serenata, "Serenata")]
    [InlineData(EventType.Arraial, "Arraial")]
    [InlineData(EventType.Convivio, "Convívio")]
    [InlineData(EventType.Nerba, "NERBA")]
    [InlineData(EventType.Missa, "Missa")]
    [InlineData(EventType.Batizado, "Batizado")]
    public void GetDisplayName_ReturnsCorrectPortugueseName(EventType eventType, string expectedDisplayName)
    {
        // Act
        var displayName = eventType.GetDisplayName();

        // Assert
        displayName.Should().Be(expectedDisplayName);
    }

    [Fact]
    public void GetDisplayName_AllEventTypesHaveDisplayNames()
    {
        // Arrange
        var allEventTypes = Enum.GetValues<EventType>();

        // Act & Assert
        foreach (var eventType in allEventTypes)
        {
            var displayName = eventType.GetDisplayName();
            displayName.Should().NotBeNullOrEmpty();
        }
    }
}
