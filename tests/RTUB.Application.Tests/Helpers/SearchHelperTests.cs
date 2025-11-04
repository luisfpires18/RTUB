using FluentAssertions;
using RTUB.Application.Helpers;

namespace RTUB.Application.Tests.Helpers;

/// <summary>
/// Unit tests for SearchHelper
/// Tests search and filter functionality
/// </summary>
public class SearchHelperTests
{
    private class TestItem
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Value { get; set; }
    }

    [Fact]
    public void Filter_WithMatchingTerm_ReturnsFilteredItems()
    {
        // Arrange
        var helper = new SearchHelper<TestItem> { SearchTerm = "test" };
        var items = new List<TestItem>
        {
            new() { Name = "Test Item" },
            new() { Name = "Another Test" },
            new() { Name = "Something Else" }
        };

        // Act
        var result = helper.Filter(items, x => x.Name);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(x => x.Name.ToLower().Contains("test"));
    }

    [Fact]
    public void Filter_CaseInsensitive_MatchesDifferentCases()
    {
        // Arrange
        var helper = new SearchHelper<TestItem> { SearchTerm = "TEST" };
        var items = new List<TestItem>
        {
            new() { Name = "test item" },
            new() { Name = "Test Item" },
            new() { Name = "TEST ITEM" }
        };

        // Act
        var result = helper.Filter(items, x => x.Name, caseSensitive: false);

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public void Filter_CaseSensitive_MatchesExactCase()
    {
        // Arrange
        var helper = new SearchHelper<TestItem> { SearchTerm = "Test" };
        var items = new List<TestItem>
        {
            new() { Name = "test item" },
            new() { Name = "Test Item" },
            new() { Name = "TEST ITEM" }
        };

        // Act
        var result = helper.Filter(items, x => x.Name, caseSensitive: true);

        // Assert
        result.Should().HaveCount(1);
        result.First().Name.Should().Be("Test Item");
    }

    [Fact]
    public void Filter_WithEmptySearchTerm_ReturnsAllItems()
    {
        // Arrange
        var helper = new SearchHelper<TestItem> { SearchTerm = "" };
        var items = new List<TestItem>
        {
            new() { Name = "Item 1" },
            new() { Name = "Item 2" }
        };

        // Act
        var result = helper.Filter(items, x => x.Name);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public void Filter_WithNullItems_ReturnsEmptyList()
    {
        // Arrange
        var helper = new SearchHelper<TestItem> { SearchTerm = "test" };

        // Act
        var result = helper.Filter(null!, x => x.Name);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void FilterMultiple_WithMultipleSelectors_MatchesAny()
    {
        // Arrange
        var helper = new SearchHelper<TestItem> { SearchTerm = "test" };
        var items = new List<TestItem>
        {
            new() { Name = "Test Item", Description = "Description" },
            new() { Name = "Item", Description = "Test Description" },
            new() { Name = "Other", Description = "Something" }
        };
        var selectors = new List<Func<TestItem, string>>
        {
            x => x.Name,
            x => x.Description
        };

        // Act
        var result = helper.FilterMultiple(items, selectors);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public void FilterMultiple_WithMultipleSearchTerms_RequiresAll()
    {
        // Arrange
        var helper = new SearchHelper<TestItem> { SearchTerm = "test item" };
        var items = new List<TestItem>
        {
            new() { Name = "Test Item", Description = "Description" },
            new() { Name = "Test", Description = "Item Description" },
            new() { Name = "Other", Description = "Something" }
        };
        var selectors = new List<Func<TestItem, string>>
        {
            x => x.Name,
            x => x.Description
        };

        // Act
        var result = helper.FilterMultiple(items, selectors);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public void FilterAllTerms_WithMultipleWords_RequiresAllInSameField()
    {
        // Arrange
        var helper = new SearchHelper<TestItem> { SearchTerm = "test item" };
        var items = new List<TestItem>
        {
            new() { Name = "Test Item Name" },
            new() { Name = "Test Something" },
            new() { Name = "Item Test" }
        };

        // Act
        var result = helper.FilterAllTerms(items, x => x.Name);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(x => x.Name == "Test Item Name");
        result.Should().Contain(x => x.Name == "Item Test");
    }

    [Fact]
    public void FilterAllTerms_WithSingleWord_WorksLikeFilter()
    {
        // Arrange
        var helper = new SearchHelper<TestItem> { SearchTerm = "test" };
        var items = new List<TestItem>
        {
            new() { Name = "Test Item" },
            new() { Name = "Another Test" },
            new() { Name = "Something" }
        };

        // Act
        var result = helper.FilterAllTerms(items, x => x.Name);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public void Clear_RemovesSearchTerm()
    {
        // Arrange
        var helper = new SearchHelper<TestItem> { SearchTerm = "test" };

        // Act
        helper.Clear();

        // Assert
        helper.SearchTerm.Should().BeEmpty();
        helper.IsSearching.Should().BeFalse();
    }

    [Fact]
    public void IsSearching_WithEmptyTerm_ReturnsFalse()
    {
        // Arrange
        var helper = new SearchHelper<TestItem> { SearchTerm = "" };

        // Act & Assert
        helper.IsSearching.Should().BeFalse();
    }

    [Fact]
    public void IsSearching_WithTerm_ReturnsTrue()
    {
        // Arrange
        var helper = new SearchHelper<TestItem> { SearchTerm = "test" };

        // Act & Assert
        helper.IsSearching.Should().BeTrue();
    }
}
