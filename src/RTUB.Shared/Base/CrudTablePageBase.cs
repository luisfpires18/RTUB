using Microsoft.AspNetCore.Components;
using RTUB.Application.Helpers;

namespace RTUB.Shared;

/// <summary>
/// Base class for CRUD table pages that provides common functionality for
/// search, sorting, and pagination of entity lists.
/// </summary>
/// <typeparam name="TEntity">The entity type to manage</typeparam>
public abstract class CrudTablePageBase<TEntity> : ComponentBase
{
    // Helper instances - using private fields to prevent Blazor serialization
    private readonly SearchHelper<TEntity> _searchHelper = new();
    private readonly SortableTableHelper<TEntity> _sortHelper = new();
    private readonly PaginationHelper<TEntity> _paginationHelper = new() { PageSize = 50 };

    // Expose helpers through protected properties
    protected SearchHelper<TEntity> SearchHelper => _searchHelper;
    protected SortableTableHelper<TEntity> SortHelper => _sortHelper;
    protected PaginationHelper<TEntity> PaginationHelper => _paginationHelper;

    // Data lists
    protected List<TEntity>? AllItems { get; set; }
    protected List<TEntity> FilteredItems { get; set; } = new();
    protected List<TEntity> PaginatedItems { get; set; } = new();

    // Search term
    protected string SearchTerm { get; set; } = string.Empty;

    /// <summary>
    /// Gets the column selectors for sorting. Must be implemented by derived classes.
    /// </summary>
    protected abstract Dictionary<string, Func<TEntity, IComparable>> GetSortColumnSelectors();

    /// <summary>
    /// Gets the search selectors for filtering. Must be implemented by derived classes.
    /// </summary>
    protected abstract List<Func<TEntity, string>> GetSearchSelectors();

    /// <summary>
    /// Loads all items from the data source. Must be implemented by derived classes.
    /// </summary>
    protected abstract Task LoadItemsAsync();

    /// <summary>
    /// Handles search term changes and updates filtered data
    /// </summary>
    protected void UpdateSearch(string term)
    {
        SearchTerm = term;
        ApplyFiltersAndPagination();
    }

    /// <summary>
    /// Handles sort column changes
    /// </summary>
    protected void SortBy(string column)
    {
        SortHelper.ChangeSortColumn(column);
        ApplyFiltersAndPagination();
    }

    /// <summary>
    /// Handles page changes
    /// </summary>
    protected void ChangePage(int page)
    {
        PaginationHelper.CurrentPage = page;
        UpdatePagination();
    }

    /// <summary>
    /// Handles page size changes
    /// </summary>
    protected void ChangePageSize(int size)
    {
        PaginationHelper.PageSize = size;
        PaginationHelper.CurrentPage = 1;
        UpdatePagination();
    }

    /// <summary>
    /// Applies search filter, sorting, and pagination in sequence
    /// </summary>
    protected virtual void ApplyFiltersAndPagination()
    {
        if (AllItems == null) return;

        // Apply search filter
        SearchHelper.SearchTerm = SearchTerm;
        FilteredItems = SearchHelper.FilterMultiple(AllItems, GetSearchSelectors());

        // Apply sorting
        FilteredItems = SortHelper.ApplySort(FilteredItems, GetSortColumnSelectors());

        // Apply pagination
        UpdatePagination();
    }

    /// <summary>
    /// Updates the paginated data based on filtered items
    /// </summary>
    protected virtual void UpdatePagination()
    {
        PaginatedItems = PaginationHelper.GetPageData(FilteredItems);
    }

    /// <summary>
    /// Reloads data and refreshes the view
    /// </summary>
    protected async Task RefreshDataAsync()
    {
        await LoadItemsAsync();
        ApplyFiltersAndPagination();
        StateHasChanged();
    }
}
