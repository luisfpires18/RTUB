using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// Tests for the Alert component to ensure alert messages display and behave correctly
/// </summary>
public class AlertTests : BunitContext
{
    [Fact]
    public void Alert_DoesNotRender_WhenNoMessageAndNoContent()
    {
        // Arrange & Act
        var cut = Render<Alert>(parameters => parameters
            .Add(p => p.Message, string.Empty));

        // Assert
        cut.Markup.Should().BeEmpty("alert should not render without message or content");
    }

    [Fact]
    public void Alert_Renders_WhenMessageProvided()
    {
        // Arrange
        var message = "Success message";

        // Act
        var cut = Render<Alert>(parameters => parameters
            .Add(p => p.Message, message));

        // Assert
        cut.Markup.Should().Contain(message, "alert should display the message");
        cut.Markup.Should().Contain("alert", "alert should have alert class");
    }

    [Fact]
    public void Alert_Renders_WhenChildContentProvided()
    {
        // Arrange
        var content = "Custom alert content";

        // Act
        var cut = Render<Alert>(parameters => parameters
            .Add(p => p.ChildContent, builder => builder.AddContent(0, content)));

        // Assert
        cut.Markup.Should().Contain(content, "alert should display child content");
    }

    [Fact]
    public void Alert_AppliesSuccessClass_WhenTypeIsSuccess()
    {
        // Arrange & Act
        var cut = Render<Alert>(parameters => parameters
            .Add(p => p.Message, "Success")
            .Add(p => p.Type, Alert.AlertType.Success));

        // Assert
        cut.Markup.Should().Contain("alert-success", "alert should have success class");
    }

    [Fact]
    public void Alert_AppliesErrorClass_WhenTypeIsError()
    {
        // Arrange & Act
        var cut = Render<Alert>(parameters => parameters
            .Add(p => p.Message, "Error")
            .Add(p => p.Type, Alert.AlertType.Error));

        // Assert
        cut.Markup.Should().Contain("alert-danger", "alert should have danger class for error type");
    }

    [Fact]
    public void Alert_AppliesWarningClass_WhenTypeIsWarning()
    {
        // Arrange & Act
        var cut = Render<Alert>(parameters => parameters
            .Add(p => p.Message, "Warning")
            .Add(p => p.Type, Alert.AlertType.Warning));

        // Assert
        cut.Markup.Should().Contain("alert-warning", "alert should have warning class");
    }

    [Fact]
    public void Alert_AppliesInfoClass_WhenTypeIsInfo()
    {
        // Arrange & Act
        var cut = Render<Alert>(parameters => parameters
            .Add(p => p.Message, "Info")
            .Add(p => p.Type, Alert.AlertType.Info));

        // Assert
        cut.Markup.Should().Contain("alert-info", "alert should have info class");
    }

    [Fact]
    public void Alert_AppliesPurpleClass_WhenTypeIsPurple()
    {
        // Arrange & Act
        var cut = Render<Alert>(parameters => parameters
            .Add(p => p.Message, "Purple")
            .Add(p => p.Type, Alert.AlertType.Purple));

        // Assert
        cut.Markup.Should().Contain("alert-purple", "alert should have purple class");
    }

    [Fact]
    public void Alert_AppliesInfoClass_ByDefault()
    {
        // Arrange & Act
        var cut = Render<Alert>(parameters => parameters
            .Add(p => p.Message, "Default alert"));

        // Assert
        cut.Markup.Should().Contain("alert-info", "alert should have info class by default");
    }

    [Fact]
    public void Alert_DoesNotShowDismissButton_ByDefault()
    {
        // Arrange & Act
        var cut = Render<Alert>(parameters => parameters
            .Add(p => p.Message, "Test"));

        // Assert
        cut.Markup.Should().NotContain("btn-close", "alert should not have dismiss button by default");
        cut.Markup.Should().NotContain("alert-dismissible", "alert should not have dismissible class by default");
    }

    [Fact]
    public void Alert_ShowsDismissButton_WhenDismissibleIsTrue()
    {
        // Arrange & Act
        var cut = Render<Alert>(parameters => parameters
            .Add(p => p.Message, "Test")
            .Add(p => p.Dismissible, true));

        // Assert
        cut.Markup.Should().Contain("btn-close", "alert should have dismiss button");
        cut.Markup.Should().Contain("alert-dismissible", "alert should have dismissible class");
    }

    [Fact]
    public void Alert_RendersIcon_WhenProvided()
    {
        // Arrange
        var iconClass = "bi-check-circle";

        // Act
        var cut = Render<Alert>(parameters => parameters
            .Add(p => p.Message, "Success")
            .Add(p => p.Icon, iconClass));

        // Assert
        cut.Markup.Should().Contain(iconClass, "alert should display the icon");
    }

    [Fact]
    public void Alert_UsesTheIntentIcon_WhenNoneProvided()
    {
        var cut = Render<Alert>(parameters => parameters
            .Add(p => p.Message, "Test")
            .Add(p => p.Type, Alert.AlertType.Success));

        var icon = cut.Find(".rtub-alert__icon");
        icon.ClassList.Should().Contain("bi-check-circle-fill");
        icon.GetAttribute("aria-hidden").Should().Be("true");
    }

    [Fact]
    public void Alert_Purple_HasNoIcon_WhenNoneProvided()
    {
        var cut = Render<Alert>(parameters => parameters
            .Add(p => p.Message, "Test")
            .Add(p => p.Type, Alert.AlertType.Purple));

        cut.FindAll(".rtub-alert__icon").Should().BeEmpty();
    }

    [Fact]
    public void Alert_DismissButton_RemovesMessage()
    {
        // Arrange
        var cut = Render<Alert>(parameters => parameters
            .Add(p => p.Message, "Dismissible message")
            .Add(p => p.Dismissible, true));

        // Assert initial state
        cut.Markup.Should().Contain("Dismissible message", "message should be visible initially");

        // Act
        var dismissButton = cut.Find("button.btn-close");
        dismissButton.Click();

        // Assert after dismiss
        cut.Markup.Should().BeEmpty("alert should be removed after dismiss");
    }

    [Fact]
    public void Alert_DismissButton_InvokesOnDismissCallback()
    {
        // Arrange
        bool onDismissCalled = false;

        var cut = Render<Alert>(parameters => parameters
            .Add(p => p.Message, "Test")
            .Add(p => p.Dismissible, true)
            .Add(p => p.OnDismiss, EventCallback.Factory.Create(this, () =>
            {
                onDismissCalled = true;
            })));

        // Act
        var dismissButton = cut.Find("button.btn-close");
        dismissButton.Click();

        // Assert
        onDismissCalled.Should().BeTrue("OnDismiss callback should be invoked when dismiss button is clicked");
    }

    [Fact]
    public void Alert_CombinesMessageAndChildContent()
    {
        // Arrange
        var message = "Message text";
        var childContent = "Child content";

        // Act
        var cut = Render<Alert>(parameters => parameters
            .Add(p => p.Message, message)
            .Add(p => p.ChildContent, builder => builder.AddContent(0, childContent)));

        // Assert
        cut.Markup.Should().Contain(message, "alert should display message");
        cut.Markup.Should().Contain(childContent, "alert should display child content");
    }

    [Theory]
    [InlineData(Alert.AlertType.Error, "alert")]
    [InlineData(Alert.AlertType.Warning, "alert")]
    [InlineData(Alert.AlertType.Success, "status")]
    [InlineData(Alert.AlertType.Info, "status")]
    [InlineData(Alert.AlertType.Purple, "status")]
    public void Alert_RoleFollowsIntent(Alert.AlertType type, string role)
    {
        var cut = Render<Alert>(parameters => parameters
            .Add(p => p.Message, "Test")
            .Add(p => p.Type, type));

        cut.Find(".alert").GetAttribute("role").Should().Be(role);
    }

    [Fact]
    public void Alert_RendersTitleAboveMessage()
    {
        var cut = Render<Alert>(parameters => parameters
            .Add(p => p.Title, "Presença adicionada")
            .Add(p => p.Message, "Detalhe"));

        cut.Find(".rtub-alert__title").TextContent.Should().Be("Presença adicionada");
        cut.Find(".rtub-alert__message").TextContent.Should().Be("Detalhe");
    }

    [Fact]
    public void Alert_TitleOnly_Renders()
    {
        var cut = Render<Alert>(parameters => parameters.Add(p => p.Title, "Só título"));

        cut.Find(".alert").TextContent.Should().Contain("Só título");
    }

    [Fact]
    public void Alert_DismissButton_HasPortugueseName()
    {
        var cut = Render<Alert>(parameters => parameters
            .Add(p => p.Message, "Test")
            .Add(p => p.Dismissible, true));

        cut.Find("button.btn-close").GetAttribute("aria-label").Should().Be("Fechar mensagem");
    }

    [Fact]
    public void Alert_NewMessageAfterDismiss_ShowsAgain()
    {
        var cut = Render<Alert>(parameters => parameters
            .Add(p => p.Message, "Primeira")
            .Add(p => p.Dismissible, true));
        cut.Find("button.btn-close").Click();
        cut.FindAll(".alert").Should().BeEmpty();

        cut.Render(parameters => parameters.Add(p => p.Message, "Segunda"));

        cut.Find(".alert").TextContent.Should().Contain("Segunda");
    }

    [Fact]
    public void Alert_IconAndMessage_RenderTogether()
    {
        // Arrange
        var message = "Success message";
        var icon = "bi-check-circle";

        // Act
        var cut = Render<Alert>(parameters => parameters
            .Add(p => p.Message, message)
            .Add(p => p.Icon, icon)
            .Add(p => p.Type, Alert.AlertType.Success));

        // Assert
        cut.Markup.Should().Contain(icon, "alert should display icon");
        cut.Markup.Should().Contain(message, "alert should display message");
        cut.Markup.Should().Contain("alert-success", "alert should have success class");
    }

    [Theory]
    [InlineData(Alert.AlertType.Success, "alert-success")]
    [InlineData(Alert.AlertType.Error, "alert-danger")]
    [InlineData(Alert.AlertType.Warning, "alert-warning")]
    [InlineData(Alert.AlertType.Info, "alert-info")]
    [InlineData(Alert.AlertType.Purple, "alert-purple")]
    public void Alert_AppliesCorrectClass_ForEachType(Alert.AlertType type, string expectedClass)
    {
        // Arrange & Act
        var cut = Render<Alert>(parameters => parameters
            .Add(p => p.Message, "Test")
            .Add(p => p.Type, type));

        // Assert
        cut.Markup.Should().Contain(expectedClass, $"alert should have {expectedClass} for type {type}");
    }
}
