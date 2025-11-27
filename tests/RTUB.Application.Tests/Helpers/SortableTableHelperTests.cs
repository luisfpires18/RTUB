using FluentAssertions;
using RTUB.Application.Helpers;

namespace RTUB.Application.Tests.Helpers;

/// <summary>
/// Unit tests for SortableTableHelper
/// Tests table sorting logic with column toggling
/// </summary>
public class SortableTableHelperTests
{
    private class TestItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public DateTime Date { get; set; }
    }

    [Fact]
    public void Constructor_InitializesWithDefaults()
    {
        // Act
        var helper = new SortableTableHelper<TestItem>();

        // Assert
        helper.SortColumn.Should().BeEmpty();
        helper.SortAscending.Should().BeTrue();
    }

    [Fact]
    public void Sort_FirstClick_SortsAscending()
    {
        // Arrange
        var helper = new SortableTableHelper<TestItem>();
        var items = new List<TestItem>
        {
            new() { Id = 3, Name = "Charlie" },
            new() { Id = 1, Name = "Alice" },
            new() { Id = 2, Name = "Bob" }
        };

        // Act
        var result = helper.Sort(items, "Name", x => x.Name);

        // Assert
        result.Select(x => x.Name).Should().BeEquivalentTo(new[] { "Alice", "Bob", "Charlie" }, o => o.WithStrictOrdering());
        helper.SortColumn.Should().Be("Name");
        helper.SortAscending.Should().BeTrue();
    }

    [Fact]
    public void Sort_SecondClickSameColumn_TogglesDescending()
    {
        // Arrange
        var helper = new SortableTableHelper<TestItem>();
        var items = new List<TestItem>
        {
            new() { Id = 3, Name = "Charlie" },
            new() { Id = 1, Name = "Alice" },
            new() { Id = 2, Name = "Bob" }
        };

        // Act
        helper.Sort(items, "Name", x => x.Name);
        var result = helper.Sort(items, "Name", x => x.Name);

        // Assert
        result.Select(x => x.Name).Should().BeEquivalentTo(new[] { "Charlie", "Bob", "Alice" }, o => o.WithStrictOrdering());
        helper.SortAscending.Should().BeFalse();
    }

    [Fact]
    public void Sort_DifferentColumn_ResetsToAscending()
    {
        // Arrange
        var helper = new SortableTableHelper<TestItem>();
        var items = new List<TestItem>
        {
            new() { Id = 3, Name = "Charlie" },
            new() { Id = 1, Name = "Alice" },
            new() { Id = 2, Name = "Bob" }
        };
        helper.Sort(items, "Name", x => x.Name);
        helper.Sort(items, "Name", x => x.Name); // Now descending

        // Act
        var result = helper.Sort(items, "Id", x => x.Id);

        // Assert
        result.Select(x => x.Id).Should().BeEquivalentTo(new[] { 1, 2, 3 }, o => o.WithStrictOrdering());
        helper.SortColumn.Should().Be("Id");
        helper.SortAscending.Should().BeTrue();
    }

    [Fact]
    public void Sort_WithNullList_ReturnsEmptyList()
    {
        // Arrange
        var helper = new SortableTableHelper<TestItem>();

        // Act
        var result = helper.Sort(null!, "Name", x => x.Name);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Sort_WithEmptyList_ReturnsEmptyList()
    {
        // Arrange
        var helper = new SortableTableHelper<TestItem>();
        var items = new List<TestItem>();

        // Act
        var result = helper.Sort(items, "Name", x => x.Name);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ApplySort_WithValidSelector_SortsCorrectly()
    {
        // Arrange
        var helper = new SortableTableHelper<TestItem> { SortColumn = "Name", SortAscending = true };
        var items = new List<TestItem>
        {
            new() { Id = 3, Name = "Charlie" },
            new() { Id = 1, Name = "Alice" },
            new() { Id = 2, Name = "Bob" }
        };
        var selectors = new Dictionary<string, Func<TestItem, IComparable>>
        {
            ["Name"] = x => x.Name,
            ["Id"] = x => x.Id
        };

        // Act
        var result = helper.ApplySort(items, selectors);

        // Assert
        result.Select(x => x.Name).Should().BeEquivalentTo(new[] { "Alice", "Bob", "Charlie" }, o => o.WithStrictOrdering());
    }

    [Fact]
    public void ApplySort_WithDescending_SortsDescending()
    {
        // Arrange
        var helper = new SortableTableHelper<TestItem> { SortColumn = "Name", SortAscending = false };
        var items = new List<TestItem>
        {
            new() { Id = 3, Name = "Charlie" },
            new() { Id = 1, Name = "Alice" },
            new() { Id = 2, Name = "Bob" }
        };
        var selectors = new Dictionary<string, Func<TestItem, IComparable>>
        {
            ["Name"] = x => x.Name
        };

        // Act
        var result = helper.ApplySort(items, selectors);

        // Assert
        result.Select(x => x.Name).Should().BeEquivalentTo(new[] { "Charlie", "Bob", "Alice" }, o => o.WithStrictOrdering());
    }

    [Fact]
    public void ApplySort_WithEmptySortColumn_ReturnsOriginalOrder()
    {
        // Arrange
        var helper = new SortableTableHelper<TestItem>();
        var items = new List<TestItem>
        {
            new() { Id = 3, Name = "Charlie" },
            new() { Id = 1, Name = "Alice" },
            new() { Id = 2, Name = "Bob" }
        };
        var selectors = new Dictionary<string, Func<TestItem, IComparable>>
        {
            ["Name"] = x => x.Name
        };

        // Act
        var result = helper.ApplySort(items, selectors);

        // Assert
        result.Should().BeEquivalentTo(items, o => o.WithStrictOrdering());
    }

    [Fact]
    public void ApplySort_WithUnknownColumn_ReturnsOriginalOrder()
    {
        // Arrange
        var helper = new SortableTableHelper<TestItem> { SortColumn = "Unknown" };
        var items = new List<TestItem>
        {
            new() { Id = 3, Name = "Charlie" },
            new() { Id = 1, Name = "Alice" }
        };
        var selectors = new Dictionary<string, Func<TestItem, IComparable>>
        {
            ["Name"] = x => x.Name
        };

        // Act
        var result = helper.ApplySort(items, selectors);

        // Assert
        result.Should().BeEquivalentTo(items, o => o.WithStrictOrdering());
    }

    [Fact]
    public void ApplySort_WithNullList_ReturnsEmptyList()
    {
        // Arrange
        var helper = new SortableTableHelper<TestItem> { SortColumn = "Name" };
        var selectors = new Dictionary<string, Func<TestItem, IComparable>>
        {
            ["Name"] = x => x.Name
        };

        // Act
        var result = helper.ApplySort(null!, selectors);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ChangeSortColumn_SameColumn_TogglesDirection()
    {
        // Arrange
        var helper = new SortableTableHelper<TestItem> { SortColumn = "Name", SortAscending = true };

        // Act
        helper.ChangeSortColumn("Name");

        // Assert
        helper.SortColumn.Should().Be("Name");
        helper.SortAscending.Should().BeFalse();
    }

    [Fact]
    public void ChangeSortColumn_DifferentColumn_ResetsToAscending()
    {
        // Arrange
        var helper = new SortableTableHelper<TestItem> { SortColumn = "Name", SortAscending = false };

        // Act
        helper.ChangeSortColumn("Id");

        // Assert
        helper.SortColumn.Should().Be("Id");
        helper.SortAscending.Should().BeTrue();
    }

    [Fact]
    public void GetSortIcon_ActiveColumnAscending_ReturnsUpArrow()
    {
        // Arrange
        var helper = new SortableTableHelper<TestItem> { SortColumn = "Name", SortAscending = true };

        // Act
        var result = helper.GetSortIcon("Name");

        // Assert
        result.Should().Be("bi-arrow-up");
    }

    [Fact]
    public void GetSortIcon_ActiveColumnDescending_ReturnsDownArrow()
    {
        // Arrange
        var helper = new SortableTableHelper<TestItem> { SortColumn = "Name", SortAscending = false };

        // Act
        var result = helper.GetSortIcon("Name");

        // Assert
        result.Should().Be("bi-arrow-down");
    }

    [Fact]
    public void GetSortIcon_InactiveColumn_ReturnsEmpty()
    {
        // Arrange
        var helper = new SortableTableHelper<TestItem> { SortColumn = "Name" };

        // Act
        var result = helper.GetSortIcon("Id");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void IsActiveSortColumn_ActiveColumn_ReturnsTrue()
    {
        // Arrange
        var helper = new SortableTableHelper<TestItem> { SortColumn = "Name" };

        // Act & Assert
        helper.IsActiveSortColumn("Name").Should().BeTrue();
    }

    [Fact]
    public void IsActiveSortColumn_InactiveColumn_ReturnsFalse()
    {
        // Arrange
        var helper = new SortableTableHelper<TestItem> { SortColumn = "Name" };

        // Act & Assert
        helper.IsActiveSortColumn("Id").Should().BeFalse();
    }

    [Fact]
    public void Reset_ClearsAllState()
    {
        // Arrange
        var helper = new SortableTableHelper<TestItem> { SortColumn = "Name", SortAscending = false };

        // Act
        helper.Reset();

        // Assert
        helper.SortColumn.Should().BeEmpty();
        helper.SortAscending.Should().BeTrue();
    }

    [Fact]
    public void Sort_ByDate_SortsCorrectly()
    {
        // Arrange
        var helper = new SortableTableHelper<TestItem>();
        var items = new List<TestItem>
        {
            new() { Id = 1, Date = new DateTime(2024, 3, 15) },
            new() { Id = 2, Date = new DateTime(2024, 1, 10) },
            new() { Id = 3, Date = new DateTime(2024, 2, 20) }
        };

        // Act
        var result = helper.Sort(items, "Date", x => x.Date);

        // Assert
        result.Select(x => x.Id).Should().BeEquivalentTo(new[] { 2, 3, 1 }, o => o.WithStrictOrdering());
    }
}
