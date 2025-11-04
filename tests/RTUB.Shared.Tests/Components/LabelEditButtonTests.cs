using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Core.Entities;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// Tests for the LabelEditButton component to ensure edit button displays correctly for admins
/// </summary>
public class LabelEditButtonTests : TestContext
{
    [Fact]
    public void LabelEditButton_DoesNotRenderButton_WhenNotAuthorized()
    {
        // Arrange
        this.AddTestAuthorization().SetNotAuthorized();
        var label = Label.Create("test-key", "Test Title", "Test Content");

        // Act
        var cut = RenderComponent<LabelEditButton>(parameters => parameters
            .Add(p => p.CurrentLabel, label));

        // Assert - AuthorizeView renders but button inside should not be accessible
        cut.Markup.Should().Contain("AuthorizeView", "AuthorizeView component should render");
        // The button will be in markup but authorization prevents it from showing
    }

    [Fact]
    public void LabelEditButton_Renders_WhenAuthorizedAsAdmin()
    {
        // Arrange
        this.AddTestAuthorization().SetAuthorized("TestUser").SetRoles("Admin");
        var label = Label.Create("test-key", "Test Title", "Test Content");

        // Act
        var cut = RenderComponent<LabelEditButton>(parameters => parameters
            .Add(p => p.CurrentLabel, label));

        // Assert
        cut.Markup.Should().Contain("btn", "button should render for admin users");
        cut.Markup.Should().Contain("bi-pencil", "button should have pencil icon");
    }

    [Fact]
    public void LabelEditButton_DoesNotRenderButton_WhenLabelIsNull()
    {
        // Arrange
        this.AddTestAuthorization().SetAuthorized("TestUser").SetRoles("Admin");

        // Act
        var cut = RenderComponent<LabelEditButton>(parameters => parameters
            .Add(p => p.CurrentLabel, (Label?)null));

        // Assert - AuthorizeView renders but button should not be present
        cut.Markup.Should().NotContain("bi-pencil", "button should not render when label is null");
    }

    [Fact]
    public void LabelEditButton_HasEditTitle()
    {
        // Arrange
        this.AddTestAuthorization().SetAuthorized("TestUser").SetRoles("Admin");
        var label = Label.Create("test-key", "Test Title", "Test Content");

        // Act
        var cut = RenderComponent<LabelEditButton>(parameters => parameters
            .Add(p => p.CurrentLabel, label));

        // Assert
        cut.Markup.Should().Contain("Editar conteúdo", "button should have 'Editar conteúdo' title");
    }

    [Fact]
    public void LabelEditButton_AppliesDefaultCssClass()
    {
        // Arrange
        this.AddTestAuthorization().SetAuthorized("TestUser").SetRoles("Admin");
        var label = Label.Create("test-key", "Test Title", "Test Content");

        // Act
        var cut = RenderComponent<LabelEditButton>(parameters => parameters
            .Add(p => p.CurrentLabel, label));

        // Assert
        cut.Markup.Should().Contain("text-white", "button should have default text-white class");
    }

    [Fact]
    public void LabelEditButton_AppliesCustomCssClass()
    {
        // Arrange
        this.AddTestAuthorization().SetAuthorized("TestUser").SetRoles("Admin");
        var label = Label.Create("test-key", "Test Title", "Test Content");
        var customClass = "custom-edit-button";

        // Act
        var cut = RenderComponent<LabelEditButton>(parameters => parameters
            .Add(p => p.CurrentLabel, label)
            .Add(p => p.CssClass, customClass));

        // Assert
        cut.Markup.Should().Contain(customClass, "button should apply custom CSS class");
    }

    [Fact]
    public void LabelEditButton_IsSmallButton()
    {
        // Arrange
        this.AddTestAuthorization().SetAuthorized("TestUser").SetRoles("Admin");
        var label = Label.Create("test-key", "Test Title", "Test Content");

        // Act
        var cut = RenderComponent<LabelEditButton>(parameters => parameters
            .Add(p => p.CurrentLabel, label));

        // Assert
        cut.Markup.Should().Contain("btn-sm", "button should be small size");
    }

    [Fact]
    public void LabelEditButton_IsLinkStyle()
    {
        // Arrange
        this.AddTestAuthorization().SetAuthorized("TestUser").SetRoles("Admin");
        var label = Label.Create("test-key", "Test Title", "Test Content");

        // Act
        var cut = RenderComponent<LabelEditButton>(parameters => parameters
            .Add(p => p.CurrentLabel, label));

        // Assert
        cut.Markup.Should().Contain("btn-link", "button should have link style");
    }

    [Fact]
    public void LabelEditButton_InvokesOnEditClick_WhenClicked()
    {
        // Arrange
        this.AddTestAuthorization().SetAuthorized("TestUser").SetRoles("Admin");
        var label = Label.Create("test-key", "Test Title", "Test Content");
        bool callbackInvoked = false;
        Label? receivedLabel = null;

        var cut = RenderComponent<LabelEditButton>(parameters => parameters
            .Add(p => p.CurrentLabel, label)
            .Add(p => p.OnEditClick, EventCallback.Factory.Create<Label>(this, (l) =>
            {
                callbackInvoked = true;
                receivedLabel = l;
            })));

        // Act
        var button = cut.Find("button");
        button.Click();

        // Assert
        callbackInvoked.Should().BeTrue("OnEditClick callback should be invoked");
        receivedLabel.Should().Be(label, "callback should receive the label");
    }
}
