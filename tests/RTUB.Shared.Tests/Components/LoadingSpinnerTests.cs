using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// Tests for the LoadingSpinner component to ensure loading states display correctly
/// </summary>
public class LoadingSpinnerTests : TestContext
{
    [Fact]
    public void LoadingSpinner_Renders_WhenShowIsTrue()
    {
        // Arrange & Act
        var cut = RenderComponent<LoadingSpinner>(parameters => parameters
            .Add(p => p.Show, true));

        // Assert
        cut.Markup.Should().Contain("spinner-border", "spinner should render with border class");
        cut.Markup.Should().Contain("role=\"status\"", "spinner should have status role for accessibility");
    }

    [Fact]
    public void LoadingSpinner_DoesNotRender_WhenShowIsFalse()
    {
        // Arrange & Act
        var cut = RenderComponent<LoadingSpinner>(parameters => parameters
            .Add(p => p.Show, false));

        // Assert
        cut.Markup.Should().BeEmpty("spinner should not render when Show is false");
    }

    [Fact]
    public void LoadingSpinner_ShowsDefaultMessage()
    {
        // Arrange & Act
        var cut = RenderComponent<LoadingSpinner>(parameters => parameters
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
        var cut = RenderComponent<LoadingSpinner>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Message, customMessage));

        // Assert
        cut.Markup.Should().Contain(customMessage, "custom message should be displayed");
    }

    [Fact]
    public void LoadingSpinner_HidesMessage_WhenShowMessageIsFalse()
    {
        // Arrange & Act
        var cut = RenderComponent<LoadingSpinner>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.ShowMessage, false));

        // Assert
        cut.Markup.Should().NotContain("<p", "message paragraph should not render when ShowMessage is false");
    }

    [Fact]
    public void LoadingSpinner_UsesBorderType_ByDefault()
    {
        // Arrange & Act
        var cut = RenderComponent<LoadingSpinner>(parameters => parameters
            .Add(p => p.Show, true));

        // Assert
        cut.Markup.Should().Contain("spinner-border", "default spinner type should be border");
    }

    [Fact]
    public void LoadingSpinner_UsesGrowType_WhenTypeIsGrow()
    {
        // Arrange & Act
        var cut = RenderComponent<LoadingSpinner>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Type, LoadingSpinner.SpinnerVariant.Grow));

        // Assert
        cut.Markup.Should().Contain("spinner-grow", "spinner should use grow type");
    }

    [Fact]
    public void LoadingSpinner_AppliesSmallSize_WhenSizeIsSmall()
    {
        // Arrange & Act
        var cut = RenderComponent<LoadingSpinner>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Size, LoadingSpinner.SpinnerSize.Small));

        // Assert
        cut.Markup.Should().Contain("spinner-border-sm", "spinner should have small size class");
    }

    [Fact]
    public void LoadingSpinner_AppliesLargeSize_WhenSizeIsLarge()
    {
        // Arrange & Act
        var cut = RenderComponent<LoadingSpinner>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Size, LoadingSpinner.SpinnerSize.Large));

        // Assert
        cut.Markup.Should().Contain("spinner-border-lg", "spinner should have large size class");
    }

    [Fact]
    public void LoadingSpinner_IsCentered_ByDefault()
    {
        // Arrange & Act
        var cut = RenderComponent<LoadingSpinner>(parameters => parameters
            .Add(p => p.Show, true));

        // Assert
        cut.Markup.Should().Contain("text-center", "spinner should be centered by default");
    }

    [Fact]
    public void LoadingSpinner_CanBeNotCentered()
    {
        // Arrange & Act
        var cut = RenderComponent<LoadingSpinner>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Centered, false));

        // Assert
        cut.Markup.Should().NotContain("text-center", "spinner should not be centered when Centered is false");
    }

    [Fact]
    public void LoadingSpinner_AppliesPrimaryColor_ByDefault()
    {
        // Arrange & Act
        var cut = RenderComponent<LoadingSpinner>(parameters => parameters
            .Add(p => p.Show, true));

        // Assert
        cut.Markup.Should().Contain("text-primary", "spinner should have primary color by default");
    }

    [Theory]
    [InlineData(LoadingSpinner.SpinnerColor.Secondary, "text-secondary")]
    [InlineData(LoadingSpinner.SpinnerColor.Success, "text-success")]
    [InlineData(LoadingSpinner.SpinnerColor.Danger, "text-danger")]
    [InlineData(LoadingSpinner.SpinnerColor.Warning, "text-warning")]
    [InlineData(LoadingSpinner.SpinnerColor.Info, "text-info")]
    [InlineData(LoadingSpinner.SpinnerColor.Dark, "text-dark")]
    [InlineData(LoadingSpinner.SpinnerColor.Purple, "text-purple")]
    public void LoadingSpinner_AppliesCorrectColor_ForEachColorType(LoadingSpinner.SpinnerColor color, string expectedClass)
    {
        // Arrange & Act
        var cut = RenderComponent<LoadingSpinner>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.Color, color));

        // Assert
        cut.Markup.Should().Contain(expectedClass, $"spinner should have {expectedClass} for color {color}");
    }

    [Fact]
    public void LoadingSpinner_HasAccessibleLabel_ByDefault()
    {
        // Arrange & Act
        var cut = RenderComponent<LoadingSpinner>(parameters => parameters
            .Add(p => p.Show, true));

        // Assert
        cut.Markup.Should().Contain("visually-hidden", "accessible label should be hidden visually");
        cut.Markup.Should().Contain("A carregar...", "accessible label should have default text");
    }

    [Fact]
    public void LoadingSpinner_ShowsLabel_WhenShowLabelIsTrue()
    {
        // Arrange & Act
        var cut = RenderComponent<LoadingSpinner>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.ShowLabel, true)
            .Add(p => p.Type, LoadingSpinner.SpinnerVariant.Grow));

        // Assert - Label should be visible (not visually-hidden for grow type)
        var markup = cut.Markup;
        markup.Should().Contain("A carregar...", "label should be displayed");
    }

    [Fact]
    public void LoadingSpinner_AppliesAdditionalContainerClass()
    {
        // Arrange
        var additionalClass = "my-custom-class";

        // Act
        var cut = RenderComponent<LoadingSpinner>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.AdditionalContainerClass, additionalClass));

        // Assert
        cut.Markup.Should().Contain(additionalClass, "additional container class should be applied");
    }

    [Fact]
    public void LoadingSpinner_ShowsRendersByDefault()
    {
        // Arrange & Act - Show defaults to true
        var cut = RenderComponent<LoadingSpinner>();

        // Assert
        cut.Markup.Should().NotBeEmpty("spinner should render by default");
        cut.Markup.Should().Contain("spinner-border", "spinner should be visible");
    }
}
