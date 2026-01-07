using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// Tests for the LoadableContent component to ensure loading, empty, and content states display correctly
/// </summary>
public class LoadableContentTests : TestContext
{
    [Fact]
    public void LoadableContent_IsLoading_ShowsDefaultLoadingSpinner()
    {
        // Arrange & Act
        var cut = RenderComponent<LoadableContent<string>>(parameters => parameters
            .Add(p => p.IsLoading, true)
            .Add(p => p.Items, null));

        // Assert
        cut.Markup.Should().Contain("spinner-border", "should display default LoadingSpinner when loading");
        cut.Markup.Should().Contain("role=\"status\"", "spinner should have status role for accessibility");
    }

    [Fact]
    public void LoadableContent_IsLoading_ShowsCustomLoadingContent()
    {
        // Arrange
        var customLoadingText = "Custom loading message";

        // Act
        var cut = RenderComponent<LoadableContent<string>>(parameters => parameters
            .Add(p => p.IsLoading, true)
            .Add(p => p.Items, null)
            .Add(p => p.LoadingContent, builder => builder.AddContent(0, customLoadingText)));

        // Assert
        cut.Markup.Should().Contain(customLoadingText, "should display custom LoadingContent when provided");
        cut.Markup.Should().NotContain("spinner-border", "should not display default spinner when custom LoadingContent is provided");
    }

    [Fact]
    public void LoadableContent_ItemsNull_ShowsDefaultEmptyState()
    {
        // Arrange & Act
        var cut = RenderComponent<LoadableContent<string>>(parameters => parameters
            .Add(p => p.IsLoading, false)
            .Add(p => p.Items, null));

        // Assert
        cut.Markup.Should().Contain("empty-state-container", "should display default EmptyState when Items is null");
        cut.Markup.Should().Contain("No items found", "should display default EmptyTitle");
    }

    [Fact]
    public void LoadableContent_ItemsEmpty_ShowsEmptyState()
    {
        // Arrange
        var emptyItems = Enumerable.Empty<string>();

        // Act
        var cut = RenderComponent<LoadableContent<string>>(parameters => parameters
            .Add(p => p.IsLoading, false)
            .Add(p => p.Items, emptyItems));

        // Assert
        cut.Markup.Should().Contain("empty-state-container", "should display default EmptyState when Items is empty");
        cut.Markup.Should().Contain("No items found", "should display default EmptyTitle");
    }

    [Fact]
    public void LoadableContent_ItemsEmpty_ShowsCustomEmptyContent()
    {
        // Arrange
        var customEmptyText = "No data available - custom message";
        var emptyItems = Enumerable.Empty<string>();

        // Act
        var cut = RenderComponent<LoadableContent<string>>(parameters => parameters
            .Add(p => p.IsLoading, false)
            .Add(p => p.Items, emptyItems)
            .Add(p => p.EmptyContent, builder => builder.AddContent(0, customEmptyText)));

        // Assert
        cut.Markup.Should().Contain(customEmptyText, "should display custom EmptyContent when provided");
        cut.Markup.Should().NotContain("empty-state-container", "should not display default EmptyState when custom EmptyContent is provided");
    }

    [Fact]
    public void LoadableContent_HasItems_ShowsChildContent()
    {
        // Arrange
        var items = new List<string> { "Item 1", "Item 2", "Item 3" };

        // Act
        var cut = RenderComponent<LoadableContent<string>>(parameters => parameters
            .Add(p => p.IsLoading, false)
            .Add(p => p.Items, items)
            .Add(p => p.ChildContent, (RenderFragment<IEnumerable<string>>)(itemsContext => builder =>
            {
                foreach (var item in itemsContext)
                {
                    builder.OpenElement(0, "span");
                    builder.AddContent(1, "Rendered: " + item);
                    builder.CloseElement();
                }
            })));

        // Assert
        cut.Markup.Should().Contain("Rendered: Item 1", "should display first item in ChildContent");
        cut.Markup.Should().Contain("Rendered: Item 2", "should display second item in ChildContent");
        cut.Markup.Should().Contain("Rendered: Item 3", "should display third item in ChildContent");
        cut.Markup.Should().NotContain("spinner-border", "should not display spinner when items exist");
        cut.Markup.Should().NotContain("empty-state-container", "should not display empty state when items exist");
    }

    [Fact]
    public void LoadableContent_EmptyState_UsesCustomTitle()
    {
        // Arrange
        var customTitle = "No results found for your search";
        var emptyItems = Enumerable.Empty<string>();

        // Act
        var cut = RenderComponent<LoadableContent<string>>(parameters => parameters
            .Add(p => p.IsLoading, false)
            .Add(p => p.Items, emptyItems)
            .Add(p => p.EmptyTitle, customTitle));

        // Assert
        cut.Markup.Should().Contain(customTitle, "should display custom EmptyTitle in EmptyState");
        cut.Markup.Should().NotContain("No items found", "should not display default EmptyTitle when custom is provided");
    }
}
