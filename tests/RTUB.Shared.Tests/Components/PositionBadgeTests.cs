using Bunit;
using FluentAssertions;
using RTUB.Core.Enums;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// Tests for the PositionBadge component to ensure position badges display correctly
/// </summary>
public class PositionBadgeTests : TestContext
{
    [Fact]
    public void PositionBadge_RendersMagisterPosition()
    {
        // Arrange & Act
        var cut = RenderComponent<PositionBadge>(parameters => parameters
            .Add(p => p.Position, Position.Magister));

        // Assert
        cut.Markup.Should().Contain("MAGISTER", "badge should display position name");
        cut.Markup.Should().Contain("badge-position", "position badge should have correct class");
    }

    [Fact]
    public void PositionBadge_RendersViceMagisterPosition()
    {
        // Arrange & Act
        var cut = RenderComponent<PositionBadge>(parameters => parameters
            .Add(p => p.Position, Position.ViceMagister));

        // Assert
        cut.Markup.Should().Contain("VICE-MAGISTER", "badge should display position name");
    }

    [Fact]
    public void PositionBadge_RendersSecretarioPosition()
    {
        // Arrange & Act
        var cut = RenderComponent<PositionBadge>(parameters => parameters
            .Add(p => p.Position, Position.Secretario));

        // Assert
        cut.Markup.Should().Contain("SECRETÁRIO", "badge should display position name with accent");
    }

    [Fact]
    public void PositionBadge_RendersPrimeiroTesoureiroPosition()
    {
        // Arrange & Act
        var cut = RenderComponent<PositionBadge>(parameters => parameters
            .Add(p => p.Position, Position.PrimeiroTesoureiro));

        // Assert
        cut.Markup.Should().Contain("1º TESOUREIRO", "badge should display formatted position name");
    }

    [Fact]
    public void PositionBadge_RendersSegundoTesoureiroPosition()
    {
        // Arrange & Act
        var cut = RenderComponent<PositionBadge>(parameters => parameters
            .Add(p => p.Position, Position.SegundoTesoureiro));

        // Assert
        cut.Markup.Should().Contain("2º TESOUREIRO", "badge should display formatted position name");
    }

    [Fact]
    public void PositionBadge_RendersPresidenteMesaAssembleiaPosition()
    {
        // Arrange & Act
        var cut = RenderComponent<PositionBadge>(parameters => parameters
            .Add(p => p.Position, Position.PresidenteMesaAssembleia));

        // Assert
        cut.Markup.Should().Contain("PRESIDENTE MESA ASSEMBLEIA", "badge should display full position name");
    }

    [Fact]
    public void PositionBadge_RendersPresidenteConselhoFiscalPosition()
    {
        // Arrange & Act
        var cut = RenderComponent<PositionBadge>(parameters => parameters
            .Add(p => p.Position, Position.PresidenteConselhoFiscal));

        // Assert
        cut.Markup.Should().Contain("PRESIDENTE CONSELHO FISCAL", "badge should display full position name");
    }

    [Fact]
    public void PositionBadge_HasBadgeClass()
    {
        // Arrange & Act
        var cut = RenderComponent<PositionBadge>(parameters => parameters
            .Add(p => p.Position, Position.Magister));

        // Assert
        cut.Markup.Should().Contain("badge", "should have badge class");
        cut.Markup.Should().Contain("badge-position", "should have position badge class");
    }

    [Fact]
    public void PositionBadge_AppliesAdditionalClasses()
    {
        // Arrange
        var additionalClass = "custom-position-class";

        // Act
        var cut = RenderComponent<PositionBadge>(parameters => parameters
            .Add(p => p.Position, Position.Magister)
            .Add(p => p.AdditionalClasses, additionalClass));

        // Assert
        cut.Markup.Should().Contain(additionalClass, "additional classes should be applied");
    }

    [Fact]
    public void PositionBadge_RendersAsSpan()
    {
        // Arrange & Act
        var cut = RenderComponent<PositionBadge>(parameters => parameters
            .Add(p => p.Position, Position.ViceMagister));

        // Assert
        cut.Markup.Should().StartWith("<span", "badge should render as a span element");
    }

    [Theory]
    [InlineData(Position.Magister, "MAGISTER")]
    [InlineData(Position.ViceMagister, "VICE-MAGISTER")]
    [InlineData(Position.Secretario, "SECRETÁRIO")]
    [InlineData(Position.PrimeiroTesoureiro, "1º TESOUREIRO")]
    [InlineData(Position.SegundoTesoureiro, "2º TESOUREIRO")]
    public void PositionBadge_DisplaysCorrectText_ForEachPosition(
        Position position, string expectedText)
    {
        // Arrange & Act
        var cut = RenderComponent<PositionBadge>(parameters => parameters
            .Add(p => p.Position, position));

        // Assert
        cut.Markup.Should().Contain(expectedText, $"position {position} should display {expectedText}");
        cut.Markup.Should().Contain("badge-position", $"position {position} should have badge-position class");
    }
}
