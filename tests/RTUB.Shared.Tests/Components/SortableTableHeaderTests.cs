using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// Tests for the SortableTableHeader component to ensure sorting functionality works correctly
/// </summary>
public class SortableTableHeaderTests : TestContext
{
    [Fact]
    public void SortableTableHeader_RendersHeaderText()
    {
        // Arrange
        var headerText = "Name";

        // Act
        var cut = RenderComponent<SortableTableHeader>(parameters => parameters
            .Add(p => p.HeaderText, headerText)
            .Add(p => p.SortColumn, "name"));

        // Assert
        cut.Markup.Should().Contain(headerText, "header should display the provided text");
    }

    [Fact]
    public void SortableTableHeader_HasSortableHeaderClass()
    {
        // Arrange & Act
        var cut = RenderComponent<SortableTableHeader>(parameters => parameters
            .Add(p => p.HeaderText, "Test")
            .Add(p => p.SortColumn, "test"));

        // Assert
        cut.Markup.Should().Contain("sortable-header", "header should have sortable-header class");
    }

    [Fact]
    public void SortableTableHeader_DoesNotShowIcon_WhenNotCurrentColumn()
    {
        // Arrange & Act
        var cut = RenderComponent<SortableTableHeader>(parameters => parameters
            .Add(p => p.HeaderText, "Name")
            .Add(p => p.SortColumn, "name")
            .Add(p => p.CurrentSortColumn, "email"));

        // Assert
        cut.Markup.Should().NotContain("bi-arrow", "icon should not be displayed when not the current sort column");
    }

    [Fact]
    public void SortableTableHeader_ShowsUpArrow_WhenCurrentColumnAndAscending()
    {
        // Arrange & Act
        var cut = RenderComponent<SortableTableHeader>(parameters => parameters
            .Add(p => p.HeaderText, "Name")
            .Add(p => p.SortColumn, "name")
            .Add(p => p.CurrentSortColumn, "name")
            .Add(p => p.IsSortAscending, true));

        // Assert
        cut.Markup.Should().Contain("bi-arrow-up", "up arrow should be displayed when sorting ascending");
    }

    [Fact]
    public void SortableTableHeader_ShowsDownArrow_WhenCurrentColumnAndDescending()
    {
        // Arrange & Act
        var cut = RenderComponent<SortableTableHeader>(parameters => parameters
            .Add(p => p.HeaderText, "Name")
            .Add(p => p.SortColumn, "name")
            .Add(p => p.CurrentSortColumn, "name")
            .Add(p => p.IsSortAscending, false));

        // Assert
        cut.Markup.Should().Contain("bi-arrow-down", "down arrow should be displayed when sorting descending");
    }

    [Fact]
    public void SortableTableHeader_InvokesCallback_OnClick()
    {
        // Arrange
        bool callbackInvoked = false;
        string? receivedColumn = null;

        var cut = RenderComponent<SortableTableHeader>(parameters => parameters
            .Add(p => p.HeaderText, "Name")
            .Add(p => p.SortColumn, "name")
            .Add(p => p.OnSortChanged, EventCallback.Factory.Create<string>(this, (column) =>
            {
                callbackInvoked = true;
                receivedColumn = column;
            })));

        // Act
        var th = cut.Find("th");
        th.Click();

        // Assert
        callbackInvoked.Should().BeTrue("callback should be invoked when header is clicked");
        receivedColumn.Should().Be("name", "callback should receive the sort column name");
    }

    [Fact]
    public void SortableTableHeader_DoesNotInvokeCallback_WhenCallbackNotProvided()
    {
        // Arrange & Act
        var cut = RenderComponent<SortableTableHeader>(parameters => parameters
            .Add(p => p.HeaderText, "Name")
            .Add(p => p.SortColumn, "name"));

        var th = cut.Find("th");

        // Assert - Should not throw
        var exception = Record.Exception(() => th.Click());
        exception.Should().BeNull("clicking without callback should not throw exception");
    }

    [Fact]
    public void SortableTableHeader_RendersWithAdditionalAttributes()
    {
        // Arrange
        var additionalAttributes = new Dictionary<string, object>
        {
            { "data-test-id", "name-header" },
            { "aria-label", "Sort by name" }
        };

        // Act
        var cut = RenderComponent<SortableTableHeader>(parameters => parameters
            .Add(p => p.HeaderText, "Name")
            .Add(p => p.SortColumn, "name")
            .Add(p => p.AdditionalAttributes, additionalAttributes));

        // Assert
        cut.Markup.Should().Contain("data-test-id=\"name-header\"", "additional attributes should be rendered");
        cut.Markup.Should().Contain("aria-label=\"Sort by name\"", "additional attributes should be rendered");
    }

    [Fact]
    public void SortableTableHeader_TogglesIconCorrectly()
    {
        // Arrange
        var cut = RenderComponent<SortableTableHeader>(parameters => parameters
            .Add(p => p.HeaderText, "Name")
            .Add(p => p.SortColumn, "name")
            .Add(p => p.CurrentSortColumn, "name")
            .Add(p => p.IsSortAscending, true));

        // Assert initial state
        cut.Markup.Should().Contain("bi-arrow-up", "initially should show up arrow");

        // Act - Update to descending
        cut.SetParametersAndRender(parameters => parameters
            .Add(p => p.IsSortAscending, false));

        // Assert new state
        cut.Markup.Should().Contain("bi-arrow-down", "should show down arrow after toggle");
        cut.Markup.Should().NotContain("bi-arrow-up", "should not show up arrow after toggle");
    }

    [Fact]
    public void SortableTableHeader_ChangesIcon_WhenCurrentColumnChanges()
    {
        // Arrange
        var cut = RenderComponent<SortableTableHeader>(parameters => parameters
            .Add(p => p.HeaderText, "Name")
            .Add(p => p.SortColumn, "name")
            .Add(p => p.CurrentSortColumn, "name")
            .Add(p => p.IsSortAscending, true));

        // Assert initial state - icon visible
        cut.Markup.Should().Contain("bi-arrow-up", "icon should be visible when current column");

        // Act - Change current column
        cut.SetParametersAndRender(parameters => parameters
            .Add(p => p.CurrentSortColumn, "email"));

        // Assert new state - icon hidden
        cut.Markup.Should().NotContain("bi-arrow", "icon should be hidden when not current column");
    }

    [Fact]
    public void SortableTableHeader_IsClickable()
    {
        // Arrange & Act
        var cut = RenderComponent<SortableTableHeader>(parameters => parameters
            .Add(p => p.HeaderText, "Name")
            .Add(p => p.SortColumn, "name"));

        var th = cut.Find("th");

        // Assert - The th element should be present and clickable (has onclick handler)
        th.Should().NotBeNull("table header element should exist");
    }

    [Theory]
    [InlineData("Name", "name")]
    [InlineData("Email", "email")]
    [InlineData("Created At", "createdAt")]
    public void SortableTableHeader_RendersCorrectly_WithDifferentColumns(string headerText, string sortColumn)
    {
        // Arrange & Act
        var cut = RenderComponent<SortableTableHeader>(parameters => parameters
            .Add(p => p.HeaderText, headerText)
            .Add(p => p.SortColumn, sortColumn));

        // Assert
        cut.Markup.Should().Contain(headerText, $"should display header text: {headerText}");
    }
}
