using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Shared;
using RTUB.Application.Helpers;

namespace RTUB.Shared.Tests.Base;

/// <summary>
/// Tests for CrudTablePageBase to ensure proper search, sorting, and pagination functionality
/// </summary>
public class CrudTablePageBaseTests : TestContext
{
    // Test entity for testing purposes
    public class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int Priority { get; set; }
    }

    // Concrete implementation of abstract base class for testing
    public class TestCrudTablePage : CrudTablePageBase<TestEntity>
    {
        [Parameter]
        public List<TestEntity> TestItems { get; set; } = new();

        protected override Dictionary<string, Func<TestEntity, IComparable>> GetSortColumnSelectors()
        {
            return new Dictionary<string, Func<TestEntity, IComparable>>
            {
                { "Name", e => e.Name },
                { "CreatedAt", e => e.CreatedAt },
                { "Priority", e => e.Priority }
            };
        }

        protected override List<Func<TestEntity, string>> GetSearchSelectors()
        {
            return new List<Func<TestEntity, string>>
            {
                e => e.Name,
                e => e.Description
            };
        }

        protected override Task LoadItemsAsync()
        {
            AllItems = TestItems;
            return Task.CompletedTask;
        }

        // Expose protected members for testing
        public void PublicUpdateSearch(string term) => base.UpdateSearch(term);
        public void PublicSortBy(string column) => base.SortBy(column);
        public void PublicChangePage(int page) => base.ChangePage(page);
        public void PublicChangePageSize(int size) => base.ChangePageSize(size);
        public void PublicApplyFiltersAndPagination() => base.ApplyFiltersAndPagination();
        public Task PublicRefreshDataAsync() => base.RefreshDataAsync();

        public List<TestEntity> GetAllItems() => AllItems ?? new List<TestEntity>();
        public List<TestEntity> GetFilteredItems() => FilteredItems;
        public List<TestEntity> GetPaginatedItems() => PaginatedItems;
        public string GetSearchTerm() => SearchTerm;
        public SearchHelper<TestEntity> GetSearchHelper() => SearchHelper;
        public SortableTableHelper<TestEntity> GetSortHelper() => SortHelper;
        public PaginationHelper<TestEntity> GetPaginationHelper() => PaginationHelper;
    }

    private List<TestEntity> CreateTestData(int count = 100)
    {
        var items = new List<TestEntity>();
        for (int i = 1; i <= count; i++)
        {
            items.Add(new TestEntity
            {
                Id = i,
                Name = $"Item {i}",
                Description = $"Description for item {i}",
                CreatedAt = DateTime.Now.AddDays(-i),
                Priority = i % 5
            });
        }
        return items;
    }

    [Fact]
    public void CrudTablePageBase_Initializes_WithDefaultValues()
    {
        // Arrange & Act
        var cut = RenderComponent<TestCrudTablePage>();

        // Assert
        cut.Instance.GetAllItems().Should().BeEmpty("AllItems should be empty initially");
        cut.Instance.GetFilteredItems().Should().BeEmpty("FilteredItems should be empty initially");
        cut.Instance.GetPaginatedItems().Should().BeEmpty("PaginatedItems should be empty initially");
        cut.Instance.GetSearchTerm().Should().BeEmpty("SearchTerm should be empty initially");
    }

    [Fact]
    public async Task RefreshDataAsync_LoadsDataCorrectly()
    {
        // Arrange
        var testData = CreateTestData(50);
        var cut = RenderComponent<TestCrudTablePage>(parameters => parameters
            .Add(p => p.TestItems, testData));

        // Act
        await cut.InvokeAsync(async () => await cut.Instance.PublicRefreshDataAsync());

        // Assert
        cut.Instance.GetAllItems().Should().HaveCount(50, "AllItems should contain all test data");
        cut.Instance.GetFilteredItems().Should().HaveCount(50, "FilteredItems should match AllItems when no filters applied");
    }

    [Fact]
    public async Task UpdateSearch_FiltersItems_BySearchTerm()
    {
        // Arrange
        var testData = CreateTestData(10); // Use only 10 items
        var cut = RenderComponent<TestCrudTablePage>(parameters => parameters
            .Add(p => p.TestItems, testData));
        await cut.InvokeAsync(async () => await cut.Instance.PublicRefreshDataAsync());

        // Act
        cut.Instance.PublicUpdateSearch("Item 7");

        // Assert
        cut.Instance.GetSearchTerm().Should().Be("Item 7", "search term should be updated");
        cut.Instance.GetFilteredItems().Should().NotBeEmpty("filtered items should contain matches");
        // Should only match "Item 7" 
        cut.Instance.GetFilteredItems().Should().HaveCount(1, "should find exactly one match");
        cut.Instance.GetFilteredItems().First().Name.Should().Be("Item 7");
    }

    [Fact]
    public async Task UpdateSearch_WithEmptyTerm_ShowsAllItems()
    {
        // Arrange
        var testData = CreateTestData(50);
        var cut = RenderComponent<TestCrudTablePage>(parameters => parameters
            .Add(p => p.TestItems, testData));
        await cut.InvokeAsync(async () => await cut.Instance.PublicRefreshDataAsync());
        cut.Instance.PublicUpdateSearch("Item 1");

        // Act
        cut.Instance.PublicUpdateSearch("");

        // Assert
        cut.Instance.GetSearchTerm().Should().BeEmpty("search term should be cleared");
        cut.Instance.GetFilteredItems().Should().HaveCount(50, "all items should be shown when search is cleared");
    }

    [Fact]
    public async Task SortBy_SortsItems_BySpecifiedColumn()
    {
        // Arrange
        var testData = CreateTestData(10);
        var cut = RenderComponent<TestCrudTablePage>(parameters => parameters
            .Add(p => p.TestItems, testData));
        await cut.InvokeAsync(async () => await cut.Instance.PublicRefreshDataAsync());

        // Act
        cut.Instance.PublicSortBy("Priority");

        // Assert
        cut.Instance.GetFilteredItems().Should().BeInAscendingOrder(e => e.Priority, "items should be sorted by priority");
    }

    [Fact]
    public async Task SortBy_TogglesDirection_WhenCalledTwice()
    {
        // Arrange
        var testData = CreateTestData(10);
        var cut = RenderComponent<TestCrudTablePage>(parameters => parameters
            .Add(p => p.TestItems, testData));
        await cut.InvokeAsync(async () => await cut.Instance.PublicRefreshDataAsync());

        // Act - First sort ascending
        cut.Instance.PublicSortBy("Priority");
        var firstSort = cut.Instance.GetFilteredItems().Select(e => e.Priority).ToList();

        // Act - Second sort descending
        cut.Instance.PublicSortBy("Priority");
        var secondSort = cut.Instance.GetFilteredItems().Select(e => e.Priority).ToList();

        // Assert
        firstSort.Should().BeInAscendingOrder("first sort should be ascending");
        secondSort.Should().BeInDescendingOrder("second sort should be descending");
    }

    [Fact]
    public async Task ChangePage_UpdatesCurrentPage()
    {
        // Arrange
        var testData = CreateTestData(100);
        var cut = RenderComponent<TestCrudTablePage>(parameters => parameters
            .Add(p => p.TestItems, testData));
        await cut.InvokeAsync(async () => await cut.Instance.PublicRefreshDataAsync());

        // Act
        cut.Instance.PublicChangePage(2);

        // Assert
        cut.Instance.GetPaginationHelper().CurrentPage.Should().Be(2, "current page should be updated");
        cut.Instance.GetPaginatedItems().Should().NotBeEmpty("paginated items should be updated");
    }

    [Fact]
    public async Task ChangePageSize_UpdatesPageSizeAndResetsToFirstPage()
    {
        // Arrange
        var testData = CreateTestData(100);
        var cut = RenderComponent<TestCrudTablePage>(parameters => parameters
            .Add(p => p.TestItems, testData));
        await cut.InvokeAsync(async () => await cut.Instance.PublicRefreshDataAsync());
        cut.Instance.PublicChangePage(3);

        // Act
        cut.Instance.PublicChangePageSize(25);

        // Assert
        cut.Instance.GetPaginationHelper().PageSize.Should().Be(25, "page size should be updated");
        cut.Instance.GetPaginationHelper().CurrentPage.Should().Be(1, "current page should reset to 1");
        cut.Instance.GetPaginatedItems().Count.Should().BeLessThanOrEqualTo(25, "paginated items should respect new page size");
    }

    [Fact]
    public void ApplyFiltersAndPagination_WorksWithNullAllItems()
    {
        // Arrange
        var cut = RenderComponent<TestCrudTablePage>();

        // Act & Assert - Should not throw
        Action act = () => cut.Instance.PublicApplyFiltersAndPagination();
        act.Should().NotThrow("ApplyFiltersAndPagination should handle null AllItems gracefully");
    }

    [Fact]
    public void PaginationHelper_DefaultPageSize_Is50()
    {
        // Arrange & Act
        var cut = RenderComponent<TestCrudTablePage>();

        // Assert
        cut.Instance.GetPaginationHelper().PageSize.Should().Be(50, "default page size should be 50");
    }
}
