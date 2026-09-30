using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Shared;
using RTUB.Shared.Enums;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// Tests for the LoadingSpinner component to ensure loading states display correctly
/// </summary>
public class LoadingSpinnerTests : BunitContext
{
    [Fact]
    public void LoadingSpinner_Renders_WhenShowIsTrue()
    {
        // Arrange & Act
        var cut = Render<LoadingSpinner>(parameters => parameters
            .Add(p => p.Show, true));

        // Assert
        cut.Markup.Should().Contain("spinner-border", "spinner should render with border class");
        cut.Markup.Should().Contain("role=\"status\"", "spinner should have status role for accessibility");
    }

    [Fact]
    public void LoadingSpinner_DoesNotRender_WhenShowIsFalse()
    {
        // Arrange & Act
        var cut = Render<LoadingSpinner>(parameters => parameters
            .Add(p => p.Show, false));

        // Assert
        cut.Markup.Should().BeEmpty("spinner should not render when Show is false");
    }

    [Fact]
    public void LoadingSpinner_ShowsDefaultMessage()
    {
        // Arrange & Act
        var cut = Render<LoadingSpinner>(parameters => parameters
            .Add(p => p.Show, true));

        // Assert
        cut.Markup.Should().Contain("A carregar...", "default message should be displayed");
    }

    [Fact]
    public void LoadingSpinner_ShowsCustomMessage()
    {
        // Arrange
        var customMessage = "Loading data...";

        // Act
        var cut = Render<LoadingSpinner>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Message, customMessage));

        // Assert
        cut.Markup.Should().Contain(customMessage, "custom message should be displayed");
    }

    [Fact]
    public void LoadingSpinner_HidesMessage_WhenShowMessageIsFalse()
    {
        // Arrange & Act
        var cut = Render<LoadingSpinner>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.ShowMessage, false));

        // Assert
        cut.Markup.Should().NotContain("<p", "message paragraph should not render when ShowMessage is false");
    }

    [Fact]
    public void LoadingSpinner_UsesBorderType_ByDefault()
    {
        // Arrange & Act
        var cut = Render<LoadingSpinner>(parameters => parameters
            .Add(p => p.Show, true));

        // Assert
        cut.Markup.Should().Contain("spinner-border", "default spinner type should be border");
    }

    [Fact]
    public void LoadingSpinner_AppliesSmallSize_WhenSizeIsSmall()
    {
        // Arrange & Act
        var cut = Render<LoadingSpinner>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Size, SpinnerSize.Small));

        // Assert
        cut.Markup.Should().Contain("spinner-border-sm", "spinner should have small size class");
    }

    [Fact]
    public void LoadingSpinner_AppliesLargeSize_WhenSizeIsLarge()
    {
        // Arrange & Act
        var cut = Render<LoadingSpinner>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Size, SpinnerSize.Large));

        // Assert
        cut.Markup.Should().Contain("spinner-border-lg", "spinner should have large size class");
    }

    [Fact]
    public void LoadingSpinner_IsCentered_ByDefault()
    {
        // Arrange & Act
        var cut = Render<LoadingSpinner>(parameters => parameters
            .Add(p => p.Show, true));

        // Assert
        cut.Markup.Should().Contain("text-center", "spinner should be centered by default");
    }

    [Fact]
    public void LoadingSpinner_CanBeNotCentered()
    {
        // Arrange & Act
        var cut = Render<LoadingSpinner>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Centered, false));

        // Assert
        cut.Markup.Should().NotContain("text-center", "spinner should not be centered when Centered is false");
    }

    [Fact]
    public void LoadingSpinner_AppliesPrimaryColor_ByDefault()
    {
        // Arrange & Act
        var cut = Render<LoadingSpinner>(parameters => parameters
            .Add(p => p.Show, true));

        // Assert
        cut.Markup.Should().Contain("text-primary", "spinner should have primary color by default");
    }

    [Theory]
    [InlineData(SpinnerColor.Secondary, "text-secondary")]
    [InlineData(SpinnerColor.Success, "text-success")]
    [InlineData(SpinnerColor.Danger, "text-danger")]
    [InlineData(SpinnerColor.Warning, "text-warning")]
    [InlineData(SpinnerColor.Info, "text-info")]
    [InlineData(SpinnerColor.Dark, "text-dark")]
    [InlineData(SpinnerColor.Purple, "text-purple")]
    public void LoadingSpinner_AppliesCorrectColor_ForEachColorType(SpinnerColor color, string expectedClass)
    {
        // Arrange & Act
        var cut = Render<LoadingSpinner>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Color, color));

        // Assert
        cut.Markup.Should().Contain(expectedClass, $"spinner should have {expectedClass} for color {color}");
    }

    [Fact]
    public void LoadingSpinner_AppliesAdditionalContainerClass()
    {
        // Arrange
        var additionalClass = "my-custom-class";

        // Act
        var cut = Render<LoadingSpinner>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.AdditionalContainerClass, additionalClass));

        // Assert
        cut.Markup.Should().Contain(additionalClass, "additional container class should be applied");
    }

    [Fact]
    public void LoadingSpinner_ShowsRendersByDefault()
    {
        // Arrange & Act - Show defaults to true
        var cut = Render<LoadingSpinner>();

        // Assert
        cut.Markup.Should().NotBeEmpty("spinner should render by default");
        cut.Markup.Should().Contain("spinner-border", "spinner should be visible");
    }

    [Fact]
    public void LoadingSpinner_IsOneStatus_AnnouncingItsMessageOnce()
    {
        var cut = Render<LoadingSpinner>(parameters => parameters
            .Add(p => p.Message, "A carregar ensaios..."));

        var status = cut.Find("[role='status']");
        status.TextContent.Trim().Should().Be("A carregar ensaios...");
        cut.Find(".spinner-border").GetAttribute("aria-hidden").Should().Be("true");
        cut.FindAll("[role='status']").Should().ContainSingle();
    }

    [Fact]
    public void LoadingSpinner_WithoutVisibleMessage_KeepsAnAccessibleLabel()
    {
        var cut = Render<LoadingSpinner>(parameters => parameters
            .Add(p => p.ShowMessage, false)
            .Add(p => p.Label, "A carregar membros"));

        var hidden = cut.Find("[role='status'] .visually-hidden");
        hidden.TextContent.Should().Be("A carregar membros");
    }
}
