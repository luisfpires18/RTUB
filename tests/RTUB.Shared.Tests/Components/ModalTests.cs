using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// Tests for the Modal component to ensure proper rendering, behavior, and interactions
/// </summary>
public class ModalTests : TestContext
{
    [Fact]
    public void Modal_WhenShowIsFalse_DoesNotRender()
    {
        // Arrange & Act
        var cut = RenderComponent<Modal>(parameters => parameters
            .Add(p => p.Show, false)
            .Add(p => p.Title, "Test Modal"));

        // Assert
        cut.Markup.Should().BeEmpty("modal should not render when Show is false");
    }

    [Fact]
    public void Modal_WhenShowIsTrue_Renders()
    {
        // Arrange & Act
        var cut = RenderComponent<Modal>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Title, "Test Modal"));

        // Assert
        cut.Markup.Should().Contain("modal-backdrop", "backdrop should be rendered");
        cut.Markup.Should().Contain("modal show d-block", "modal should have show and d-block classes");
    }

    [Fact]
    public void Modal_RendersTitle_WhenProvided()
    {
        // Arrange
        var expectedTitle = "My Test Modal";

        // Act
        var cut = RenderComponent<Modal>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Title, expectedTitle));

        // Assert
        cut.Markup.Should().Contain(expectedTitle, "modal should display the provided title");
        cut.Markup.Should().Contain("modal-header", "modal should have header section");
    }

    [Fact]
    public void Modal_RendersBodyContent_WhenProvided()
    {
        // Arrange
        var bodyText = "This is the modal body content";

        // Act
        var cut = RenderComponent<Modal>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.BodyContent, builder => builder.AddContent(0, bodyText)));

        // Assert
        cut.Markup.Should().Contain(bodyText, "modal should display body content");
        cut.Markup.Should().Contain("modal-body", "modal should have body section");
    }

    [Fact]
    public void Modal_RendersFooterContent_WhenProvided()
    {
        // Arrange
        var footerText = "Footer content";

        // Act
        var cut = RenderComponent<Modal>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.FooterContent, builder => builder.AddContent(0, footerText)));

        // Assert
        cut.Markup.Should().Contain(footerText, "modal should display footer content");
        cut.Markup.Should().Contain("modal-footer", "modal should have footer section");
    }

    [Fact]
    public void Modal_DoesNotRenderFooter_WhenFooterContentIsNull()
    {
        // Arrange & Act
        var cut = RenderComponent<Modal>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Title, "Test"));

        // Assert
        cut.Markup.Should().NotContain("modal-footer", "modal should not have footer when FooterContent is null");
    }

    [Fact]
    public void Modal_ShowsCloseButton_ByDefault()
    {
        // Arrange & Act
        var cut = RenderComponent<Modal>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Title, "Test"));

        // Assert
        cut.Markup.Should().Contain("btn-close", "modal should have close button by default");
    }

    [Fact]
    public void Modal_HidesCloseButton_WhenShowCloseButtonIsFalse()
    {
        // Arrange & Act
        var cut = RenderComponent<Modal>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Title, "Test")
            .Add(p => p.ShowCloseButton, false));

        // Assert
        cut.Markup.Should().NotContain("btn-close", "modal should not have close button when ShowCloseButton is false");
    }

    [Fact]
    public void Modal_AppliesSmallSize_WhenSizeIsSmall()
    {
        // Arrange & Act
        var cut = RenderComponent<Modal>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Size, Modal.ModalSize.Small));

        // Assert
        cut.Markup.Should().Contain("modal-sm", "modal should have small size class");
    }

    [Fact]
    public void Modal_AppliesLargeSize_WhenSizeIsLarge()
    {
        // Arrange & Act
        var cut = RenderComponent<Modal>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Size, Modal.ModalSize.Large));

        // Assert
        cut.Markup.Should().Contain("modal-lg", "modal should have large size class");
    }

    [Fact]
    public void Modal_AppliesExtraLargeSize_WhenSizeIsExtraLarge()
    {
        // Arrange & Act
        var cut = RenderComponent<Modal>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Size, Modal.ModalSize.ExtraLarge));

        // Assert
        cut.Markup.Should().Contain("modal-xl", "modal should have extra large size class");
    }

    [Fact]
    public void Modal_DoesNotApplySizeClass_WhenSizeIsDefault()
    {
        // Arrange & Act
        var cut = RenderComponent<Modal>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Size, Modal.ModalSize.Default));

        // Assert
        cut.Markup.Should().NotContain("modal-sm", "modal should not have small size class");
        cut.Markup.Should().NotContain("modal-lg", "modal should not have large size class");
        cut.Markup.Should().NotContain("modal-xl", "modal should not have extra large size class");
    }

    [Fact]
    public void Modal_AppliesCenteredClass_WhenCenteredIsTrue()
    {
        // Arrange & Act
        var cut = RenderComponent<Modal>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Centered, true));

        // Assert
        cut.Markup.Should().Contain("modal-dialog-centered", "modal should have centered class when Centered is true");
    }

    [Fact]
    public void Modal_DoesNotApplyCenteredClass_WhenCenteredIsFalse()
    {
        // Arrange & Act
        var cut = RenderComponent<Modal>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Centered, false));

        // Assert
        cut.Markup.Should().NotContain("modal-dialog-centered", "modal should not have centered class when Centered is false");
    }

    [Fact]
    public void Modal_CloseButton_InvokesShowChangedCallback()
    {
        // Arrange
        bool showChangedCalled = false;
        bool newShowValue = true;

        var cut = RenderComponent<Modal>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Title, "Test")
            .Add(p => p.ShowChanged, EventCallback.Factory.Create<bool>(this, (value) =>
            {
                showChangedCalled = true;
                newShowValue = value;
            })));

        // Act
        var closeButton = cut.Find("button.btn-close");
        closeButton.Click();

        // Assert
        showChangedCalled.Should().BeTrue("ShowChanged callback should be invoked");
        newShowValue.Should().BeFalse("ShowChanged callback should receive false value");
    }

    [Fact]
    public void Modal_CloseButton_InvokesOnCloseCallback()
    {
        // Arrange
        bool onCloseCalled = false;

        var cut = RenderComponent<Modal>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Title, "Test")
            .Add(p => p.OnClose, EventCallback.Factory.Create(this, () =>
            {
                onCloseCalled = true;
            })));

        // Act
        var closeButton = cut.Find("button.btn-close");
        closeButton.Click();

        // Assert
        onCloseCalled.Should().BeTrue("OnClose callback should be invoked when close button is clicked");
    }

    [Fact]
    public void Modal_RendersHeader_OnlyWhenTitleOrCloseButtonIsPresent()
    {
        // Arrange & Act - No title, no close button
        var cut1 = RenderComponent<Modal>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Title, string.Empty)
            .Add(p => p.ShowCloseButton, false));

        // Assert
        cut1.Markup.Should().NotContain("modal-header", "modal should not have header when no title and no close button");

        // Act - With title
        var cut2 = RenderComponent<Modal>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Title, "Title")
            .Add(p => p.ShowCloseButton, false));

        // Assert
        cut2.Markup.Should().Contain("modal-header", "modal should have header when title is present");

        // Act - With close button but no title
        var cut3 = RenderComponent<Modal>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Title, string.Empty)
            .Add(p => p.ShowCloseButton, true));

        // Assert
        cut3.Markup.Should().Contain("modal-header", "modal should have header when close button is present");
    }

    [Fact]
    public void Modal_CombinesSizeAndCentered_Correctly()
    {
        // Arrange & Act
        var cut = RenderComponent<Modal>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Size, Modal.ModalSize.Large)
            .Add(p => p.Centered, true));

        // Assert
        cut.Markup.Should().Contain("modal-lg", "modal should have large size class");
        cut.Markup.Should().Contain("modal-dialog-centered", "modal should have centered class");
    }
}
