using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// Tests for the EmptyTableState component to ensure empty states display correctly
/// </summary>
public class EmptyTableStateTests : TestContext
{
    [Fact]
    public void EmptyTableState_RendersDefaultMessage()
    {
        // Arrange & Act
        var cut = RenderComponent<EmptyTableState>();

        // Assert
        cut.Markup.Should().Contain("Nenhum resultado encontrado.", "default message should be displayed");
    }

    [Fact]
    public void EmptyTableState_RendersCustomMessage()
    {
        // Arrange
        var customMessage = "No items to display";

        // Act
        var cut = RenderComponent<EmptyTableState>(parameters => parameters
            .Add(p => p.Message, customMessage));

        // Assert
        cut.Markup.Should().Contain(customMessage, "custom message should be displayed");
    }

    [Fact]
    public void EmptyTableState_RendersSubMessage_WhenProvided()
    {
        // Arrange
        var subMessage = "Try adjusting your filters";

        // Act
        var cut = RenderComponent<EmptyTableState>(parameters => parameters
            .Add(p => p.SubMessage, subMessage));

        // Assert
        cut.Markup.Should().Contain(subMessage, "sub-message should be displayed");
    }

    [Fact]
    public void EmptyTableState_DoesNotRenderSubMessage_WhenNotProvided()
    {
        // Arrange & Act
        var cut = RenderComponent<EmptyTableState>();

        // Assert - Only one paragraph for main message
        cut.Markup.Should().NotContain("card-text", "sub-message should not render when not provided");
    }

    [Fact]
    public void EmptyTableState_RendersDefaultIcon()
    {
        // Arrange & Act
        var cut = RenderComponent<EmptyTableState>();

        // Assert
        cut.Markup.Should().Contain("bi-info-circle", "default icon should be displayed");
    }

    [Fact]
    public void EmptyTableState_RendersCustomIcon()
    {
        // Arrange
        var customIcon = "bi-inbox";

        // Act
        var cut = RenderComponent<EmptyTableState>(parameters => parameters
            .Add(p => p.IconClass, customIcon));

        // Assert
        cut.Markup.Should().Contain(customIcon, "custom icon should be displayed");
    }

    [Fact]
    public void EmptyTableState_DoesNotShowButton_ByDefault()
    {
        // Arrange & Act
        var cut = RenderComponent<EmptyTableState>();

        // Assert
        cut.Markup.Should().NotContain("<button", "button should not render by default");
    }

    [Fact]
    public void EmptyTableState_ShowsButton_WhenShowButtonIsTrue()
    {
        // Arrange & Act
        var cut = RenderComponent<EmptyTableState>(parameters => parameters
            .Add(p => p.ShowButton, true));

        // Assert
        cut.Markup.Should().Contain("<button", "button should render when ShowButton is true");
    }

    [Fact]
    public void EmptyTableState_RendersDefaultButtonText()
    {
        // Arrange & Act
        var cut = RenderComponent<EmptyTableState>(parameters => parameters
            .Add(p => p.ShowButton, true));

        // Assert
        cut.Markup.Should().Contain("Criar Novo", "default button text should be displayed");
    }

    [Fact]
    public void EmptyTableState_RendersCustomButtonText()
    {
        // Arrange
        var customButtonText = "Add Item";

        // Act
        var cut = RenderComponent<EmptyTableState>(parameters => parameters
            .Add(p => p.ShowButton, true)
            .Add(p => p.ButtonText, customButtonText));

        // Assert
        cut.Markup.Should().Contain(customButtonText, "custom button text should be displayed");
    }

    [Fact]
    public void EmptyTableState_AppliesDefaultButtonClass()
    {
        // Arrange & Act
        var cut = RenderComponent<EmptyTableState>(parameters => parameters
            .Add(p => p.ShowButton, true));

        // Assert
        cut.Markup.Should().Contain("btn-success", "default button class should be applied");
    }

    [Fact]
    public void EmptyTableState_AppliesCustomButtonClass()
    {
        // Arrange
        var customClass = "btn-primary";

        // Act
        var cut = RenderComponent<EmptyTableState>(parameters => parameters
            .Add(p => p.ShowButton, true)
            .Add(p => p.ButtonClass, customClass));

        // Assert
        cut.Markup.Should().Contain(customClass, "custom button class should be applied");
    }

    [Fact]
    public void EmptyTableState_RendersButtonIcon_WhenProvided()
    {
        // Arrange
        var buttonIcon = "bi-plus-lg";

        // Act
        var cut = RenderComponent<EmptyTableState>(parameters => parameters
            .Add(p => p.ShowButton, true)
            .Add(p => p.ButtonIcon, buttonIcon));

        // Assert
        cut.Markup.Should().Contain(buttonIcon, "button icon should be displayed");
    }

    [Fact]
    public void EmptyTableState_DoesNotRenderButtonIcon_WhenNotProvided()
    {
        // Arrange & Act
        var cut = RenderComponent<EmptyTableState>(parameters => parameters
            .Add(p => p.ShowButton, true));

        // Assert
        var button = cut.Find("button");
        button.InnerHtml.Should().NotContain("bi-", "button icon should not render when not provided");
    }

    [Fact]
    public void EmptyTableState_ButtonClick_InvokesCallback()
    {
        // Arrange
        bool callbackInvoked = false;

        var cut = RenderComponent<EmptyTableState>(parameters => parameters
            .Add(p => p.ShowButton, true)
            .Add(p => p.OnButtonClick, EventCallback.Factory.Create(this, () =>
            {
                callbackInvoked = true;
            })));

        // Act
        var button = cut.Find("button");
        button.Click();

        // Assert
        callbackInvoked.Should().BeTrue("OnButtonClick callback should be invoked");
    }

    [Fact]
    public void EmptyTableState_HasProperCardStructure()
    {
        // Arrange & Act
        var cut = RenderComponent<EmptyTableState>();

        // Assert
        cut.Markup.Should().Contain("card", "should have card component");
        cut.Markup.Should().Contain("card-body", "should have card body");
        cut.Markup.Should().Contain("text-center", "should be centered");
    }

    [Fact]
    public void EmptyTableState_HasShadow()
    {
        // Arrange & Act
        var cut = RenderComponent<EmptyTableState>();

        // Assert
        cut.Markup.Should().Contain("shadow-sm", "card should have shadow");
    }

    [Fact]
    public void EmptyTableState_CombinesAllElements_Correctly()
    {
        // Arrange
        var message = "No events found";
        var subMessage = "Create your first event";
        var icon = "bi-calendar-x";
        var buttonText = "Create Event";
        var buttonIcon = "bi-plus-circle";

        // Act
        var cut = RenderComponent<EmptyTableState>(parameters => parameters
            .Add(p => p.Message, message)
            .Add(p => p.SubMessage, subMessage)
            .Add(p => p.IconClass, icon)
            .Add(p => p.ShowButton, true)
            .Add(p => p.ButtonText, buttonText)
            .Add(p => p.ButtonIcon, buttonIcon));

        // Assert
        cut.Markup.Should().Contain(message, "should display message");
        cut.Markup.Should().Contain(subMessage, "should display sub-message");
        cut.Markup.Should().Contain(icon, "should display icon");
        cut.Markup.Should().Contain(buttonText, "should display button text");
        cut.Markup.Should().Contain(buttonIcon, "should display button icon");
    }
}
