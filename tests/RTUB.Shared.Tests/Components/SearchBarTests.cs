using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// Tests for the SearchBar component to ensure search functionality works correctly
/// </summary>
public class SearchBarTests : TestContext
{
    [Fact]
    public void SearchBar_RendersSearchInput()
    {
        // Arrange & Act
        var cut = RenderComponent<SearchBar>();

        // Assert
        var input = cut.Find("input[type='text']");
        input.Should().NotBeNull();
    }

    [Fact]
    public void SearchBar_ShowsSearchIcon()
    {
        var cut = RenderComponent<SearchBar>();

        cut.Markup.Should().Contain("bi-search");
    }

    [Fact]
    public void SearchBar_UsesDefaultPlaceholder()
    {
        var cut = RenderComponent<SearchBar>();

        var input = cut.Find("input");
        input.GetAttribute("placeholder").Should().Be("Pesquisar...");
    }

    [Fact]
    public void SearchBar_AllowsCustomPlaceholder()
    {
        const string placeholder = "Search for items...";

        var cut = RenderComponent<SearchBar>(parameters => parameters
            .Add(p => p.Placeholder, placeholder));

        var input = cut.Find("input");
        input.GetAttribute("placeholder").Should().Be(placeholder);
    }

    [Fact]
    public void SearchBar_DisplaysBoundValue()
    {
        const string value = "test query";

        var cut = RenderComponent<SearchBar>(parameters => parameters
            .Add(p => p.Value, value));

        cut.Find("input").GetAttribute("value").Should().Be(value);
    }

    [Fact]
    public void SearchBar_DoesNotShowClearButton_WhenValueIsEmpty()
    {
        var cut = RenderComponent<SearchBar>(parameters => parameters
            .Add(p => p.Value, string.Empty));

        cut.FindAll("button").Should().BeEmpty();
    }

    [Fact]
    public void SearchBar_ShowsClearButton_WhenValueIsPresent()
    {
        var cut = RenderComponent<SearchBar>(parameters => parameters
            .Add(p => p.Value, "test"));

        var clearButton = cut.Find("button");
        clearButton.Should().NotBeNull();
        clearButton.InnerHtml.Should().Contain("bi-x-lg");
    }

    [Fact]
    public void SearchBar_ClearButton_HasTitle()
    {
        var cut = RenderComponent<SearchBar>(parameters => parameters
            .Add(p => p.Value, "test"));

        cut.Find("button").GetAttribute("title").Should().Be("Limpar pesquisa");
    }

    [Fact]
    public void SearchBar_ClearButton_InvokesCallbacks()
    {
        string? receivedValue = null;
        bool onSearchCalled = false;
        bool onClearCalled = false;

        var cut = RenderComponent<SearchBar>(parameters => parameters
            .Add(p => p.Value, "test")
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<string>(this, v => receivedValue = v))
            .Add(p => p.OnSearch, EventCallback.Factory.Create<string>(this, _ => onSearchCalled = true))
            .Add(p => p.OnClear, EventCallback.Factory.Create(this, () => onClearCalled = true)));

        cut.Find("button").Click();

        receivedValue.Should().Be(string.Empty);
        onSearchCalled.Should().BeTrue();
        onClearCalled.Should().BeTrue();
    }

    [Fact]
    public void SearchBar_Compact_AddsCompactClass()
    {
        var cut = RenderComponent<SearchBar>(parameters => parameters
            .Add(p => p.Compact, true));

        cut.Markup.Should().Contain("search-bar-container--compact");
    }

    [Fact]
    public void SearchBar_RendersContainer()
    {
        var cut = RenderComponent<SearchBar>();

        cut.Markup.Should().Contain("search-bar-container");
        cut.Markup.Should().Contain("search-bar");
    }

    [Fact]
    public void SearchBar_SupportsCustomDebounceDelay()
    {
        var cut = RenderComponent<SearchBar>(parameters => parameters
            .Add(p => p.DebounceDelay, 500));

        cut.Find("input").Should().NotBeNull();
    }

    [Fact]
    public void SearchBar_DefaultDebounceDelay_Is300()
    {
        var cut = RenderComponent<SearchBar>();

        cut.Instance.DebounceDelay.Should().Be(300);
    }
}
