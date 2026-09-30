using Bunit;
using FluentAssertions;
using RTUB.Components;
using Xunit;

namespace RTUB.Web.Tests.Components;

/// <summary>
/// Tests for the ReconnectModal component that provides enhanced UX during Blazor Server circuit reconnection.
/// Verifies that the component renders correctly and displays appropriate reconnection UI.
/// </summary>
public class ReconnectModalTests : BunitContext
{
    [Fact]
    public void ReconnectModal_Renders_WithCorrectStructure()
    {
        // Act
        var cut = Render<ReconnectModal>();

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
        var cut = Render<ReconnectModal>();

        // Assert
        cut.Markup.Should().Contain("reconnect-icon");
        cut.Markup.Should().Contain("bi-wifi-off");
    }

    [Fact]
    public void ReconnectModal_ContainsSpinner()
    {
        // Act
        var cut = Render<ReconnectModal>();

        // Assert
        cut.Markup.Should().Contain("spinner-border");
        cut.Markup.Should().Contain("A restabelecer ligação...");
    }

    [Fact]
    public void ReconnectModal_HasCorrectAriaAttributes()
    {
        // Act
        var cut = Render<ReconnectModal>();

        // Assert
        cut.Markup.Should().Contain("role=\"status\"");
        cut.Markup.Should().Contain("visually-hidden");
    }

    /// <summary>
    /// UI refactor 037: Blazor only uses a custom reconnection UI whose element has this id; with
    /// any other id it fell back to its built-in UI, which rendered invisible in RTUB.
    /// </summary>
    [Fact]
    public void ReconnectModal_IsTheElementBlazorDrives_WithOneMessagePerState()
    {
        var cut = Render<ReconnectModal>();

        cut.Find("#components-reconnect-modal").ClassList.Should().Contain("reconnect-modal");
        cut.Find(".reconnect-message--failed").TextContent.Should().Contain("Recarregue a página");
        cut.Find(".reconnect-message--rejected").TextContent.Should().Contain("A sessão terminou no servidor");
        cut.Find(".reconnect-note").TextContent.Should().Contain("mantém-se se a ligação voltar");
        cut.Find(".reconnect-content").GetAttribute("role").Should().Be("alertdialog");
    }
}
