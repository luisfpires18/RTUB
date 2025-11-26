using Bunit;
using FluentAssertions;
using RTUB.Components;
using Xunit;

namespace RTUB.Web.Tests.Components;

/// <summary>
/// Tests for the ReconnectModal component that provides enhanced UX during Blazor Server circuit reconnection.
/// Verifies that the component renders correctly and displays appropriate reconnection UI.
/// </summary>
public class ReconnectModalTests : TestContext
{
    [Fact]
    public void ReconnectModal_Renders_WithCorrectStructure()
    {
        // Act
        var cut = RenderComponent<ReconnectModal>();

        // Assert
        cut.Markup.Should().Contain("reconnect-modal");
        cut.Markup.Should().Contain("reconnect-content");
        cut.Markup.Should().Contain("Ligação Perdida");
        cut.Markup.Should().Contain("A tentar restabelecer a ligação ao servidor...");
    }

    [Fact]
    public void ReconnectModal_ContainsReconnectIcon()
    {
        // Act
        var cut = RenderComponent<ReconnectModal>();

        // Assert
        cut.Markup.Should().Contain("reconnect-icon");
        cut.Markup.Should().Contain("bi-wifi-off");
    }

    [Fact]
    public void ReconnectModal_ContainsSpinner()
    {
        // Act
        var cut = RenderComponent<ReconnectModal>();

        // Assert
        cut.Markup.Should().Contain("spinner-border");
        cut.Markup.Should().Contain("A restabelecer ligação...");
    }

    [Fact]
    public void ReconnectModal_ContainsCSS_ForBlazorReconnectStates()
    {
        // Act
        var cut = RenderComponent<ReconnectModal>();

        // Assert - Check that CSS targets Blazor's automatic reconnect state classes
        cut.Markup.Should().Contain("components-reconnect-show");
        cut.Markup.Should().Contain("components-reconnect-failed");
        cut.Markup.Should().Contain("components-reconnect-rejected");
    }

    [Fact]
    public void ReconnectModal_HasCorrectAriaAttributes()
    {
        // Act
        var cut = RenderComponent<ReconnectModal>();

        // Assert
        cut.Markup.Should().Contain("role=\"status\"");
        cut.Markup.Should().Contain("visually-hidden");
    }
}
