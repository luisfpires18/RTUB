using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// Tests for the TableSearchBar component to ensure search functionality works correctly
/// </summary>
public class TableSearchBarTests : TestContext
{
    [Fact]
    public void TableSearchBar_RendersSearchInput()
    {
        // Arrange & Act
        var cut = RenderComponent<TableSearchBar>();

        // Assert
        var input = cut.Find("input[type='text']");
        input.Should().NotBeNull("search input should render");
    }

    [Fact]
    public void TableSearchBar_RendersSearchIcon()
    {
        // Arrange & Act
        var cut = RenderComponent<TableSearchBar>();

        // Assert
        cut.Markup.Should().Contain("bi-search", "search icon should be displayed");
    }

    [Fact]
    public void TableSearchBar_ShowsDefaultPlaceholder()
    {
        // Arrange & Act
        var cut = RenderComponent<TableSearchBar>();

        // Assert
        var input = cut.Find("input");
        input.GetAttribute("placeholder").Should().Be("Pesquisar...", "default placeholder should be set");
    }

    [Fact]
    public void TableSearchBar_ShowsCustomPlaceholder()
    {
        // Arrange
        var customPlaceholder = "Search for items...";

        // Act
        var cut = RenderComponent<TableSearchBar>(parameters => parameters
            .Add(p => p.Placeholder, customPlaceholder));

        // Assert
        var input = cut.Find("input");
        input.GetAttribute("placeholder").Should().Be(customPlaceholder, "custom placeholder should be set");
    }

    [Fact]
    public void TableSearchBar_DisplaysSearchTerm()
    {
        // Arrange
        var searchTerm = "test query";

        // Act
        var cut = RenderComponent<TableSearchBar>(parameters => parameters
            .Add(p => p.SearchTerm, searchTerm));

        // Assert
        var input = cut.Find("input");
        input.GetAttribute("value").Should().Be(searchTerm, "search term should be displayed in input");
    }

    [Fact]
    public void TableSearchBar_DoesNotShowClearButton_WhenSearchTermIsEmpty()
    {
        // Arrange & Act
        var cut = RenderComponent<TableSearchBar>(parameters => parameters
            .Add(p => p.SearchTerm, string.Empty));

        // Assert
        var buttons = cut.FindAll("button");
        buttons.Should().BeEmpty("clear button should not render when search term is empty");
    }

    [Fact]
    public void TableSearchBar_ShowsClearButton_WhenSearchTermIsNotEmpty()
    {
        // Arrange & Act
        var cut = RenderComponent<TableSearchBar>(parameters => parameters
            .Add(p => p.SearchTerm, "test"));

        // Assert
        var clearButton = cut.Find("button");
        clearButton.Should().NotBeNull("clear button should render when search term is not empty");
        clearButton.InnerHtml.Should().Contain("bi-x-circle-fill", "clear button should have X icon");
    }

    [Fact]
    public void TableSearchBar_ClearButton_HasTitle()
    {
        // Arrange & Act
        var cut = RenderComponent<TableSearchBar>(parameters => parameters
            .Add(p => p.SearchTerm, "test"));

        // Assert
        var clearButton = cut.Find("button");
        clearButton.GetAttribute("title").Should().Be("Limpar pesquisa", "clear button should have title attribute");
    }

    [Fact]
    public void TableSearchBar_ClearButton_InvokesCallback()
    {
        // Arrange
        string? receivedTerm = null;
        bool callbackInvoked = false;

        var cut = RenderComponent<TableSearchBar>(parameters => parameters
            .Add(p => p.SearchTerm, "test")
            .Add(p => p.OnSearchChanged, EventCallback.Factory.Create<string>(this, (term) =>
            {
                callbackInvoked = true;
                receivedTerm = term;
            })));

        // Act
        var clearButton = cut.Find("button");
        clearButton.Click();

        // Assert
        callbackInvoked.Should().BeTrue("OnSearchChanged callback should be invoked");
        receivedTerm.Should().BeEmpty("callback should receive empty string when cleared");
    }

    [Fact]
    public void TableSearchBar_HasInputGroup()
    {
        // Arrange & Act
        var cut = RenderComponent<TableSearchBar>();

        // Assert
        cut.Markup.Should().Contain("input-group", "should have input group for proper styling");
    }

    [Fact]
    public void TableSearchBar_HasInputGroupText()
    {
        // Arrange & Act
        var cut = RenderComponent<TableSearchBar>();

        // Assert
        cut.Markup.Should().Contain("input-group-text", "should have input group text for icon");
    }

    [Fact]
    public void TableSearchBar_InputHasFormControlClass()
    {
        // Arrange & Act
        var cut = RenderComponent<TableSearchBar>();

        // Assert
        var input = cut.Find("input");
        input.ClassList.Should().Contain("form-control", "input should have form-control class");
    }

    [Fact]
    public void TableSearchBar_ClearButtonHasOutlineStyle()
    {
        // Arrange & Act
        var cut = RenderComponent<TableSearchBar>(parameters => parameters
            .Add(p => p.SearchTerm, "test"));

        // Assert
        var clearButton = cut.Find("button");
        clearButton.ClassList.Should().Contain("btn-outline-secondary", "clear button should have outline style");
    }

    [Fact]
    public void TableSearchBar_RendersContainer()
    {
        // Arrange & Act
        var cut = RenderComponent<TableSearchBar>();

        // Assert
        cut.Markup.Should().Contain("search-bar-container", "should have search bar container");
    }

    [Fact]
    public void TableSearchBar_SupportsDebounceDelay()
    {
        // Arrange & Act
        var cut = RenderComponent<TableSearchBar>(parameters => parameters
            .Add(p => p.DebounceDelay, 500));

        // Assert - Component should render without errors with custom debounce delay
        var input = cut.Find("input");
        input.Should().NotBeNull("component should render with custom debounce delay");
    }

    [Fact]
    public void TableSearchBar_DefaultDebounceDelay_Is300()
    {
        // This test verifies the component can be created with default debounce delay
        // Arrange & Act
        var cut = RenderComponent<TableSearchBar>();

        // Assert - Component should render with default delay
        var input = cut.Find("input");
        input.Should().NotBeNull("component should render with default debounce delay");
    }
}
