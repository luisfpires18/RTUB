namespace RTUB.Application.Helpers;

/// <summary>
/// Generic helper class for table sorting logic
/// Provides reusable methods for sorting lists with multiple columns
/// </summary>
/// <typeparam name="T">The type of items to sort</typeparam>
public class SortableTableHelper<T>
{
    /// <summary>
    /// Current sort column identifier
    /// </summary>
    public string SortColumn { get; set; } = string.Empty;

    /// <summary>
    /// Whether sorting is ascending (true) or descending (false)
    /// </summary>
    public bool SortAscending { get; set; } = true;

    /// <summary>
    /// Sorts a list based on the current sort column and direction
    /// </summary>
    /// <param name="items">List of items to sort</param>
    /// <param name="columnName">Column identifier to sort by</param>
    /// <param name="selector">Function to extract the comparable value from each item</param>
    /// <returns>Sorted list of items</returns>
    public List<T> Sort(List<T> items, string columnName, Func<T, IComparable> selector)
    {
        if (items == null || !items.Any())
        {
            return new List<T>();
        }

        // Toggle sort direction if clicking the same column
        if (SortColumn == columnName)
        {
            SortAscending = !SortAscending;
        }
        else
        {
            SortColumn = columnName;
            SortAscending = true;
        }

        return SortAscending
            ? items.OrderBy(selector).ToList()
            : items.OrderByDescending(selector).ToList();
    }

    /// <summary>
    /// Applies sorting to a list based on current sort state
    /// </summary>
    /// <param name="items">List of items to sort</param>
    /// <param name="columnSelectors">Dictionary mapping column names to selector functions</param>
    /// <returns>Sorted list of items</returns>
    public List<T> ApplySort(List<T> items, Dictionary<string, Func<T, IComparable>> columnSelectors)
    {
        if (items == null || !items.Any())
        {
            return new List<T>();
        }

        if (string.IsNullOrEmpty(SortColumn) || !columnSelectors.ContainsKey(SortColumn))
        {
            return items;
        }

        var selector = columnSelectors[SortColumn];
        return SortAscending
            ? items.OrderBy(selector).ToList()
            : items.OrderByDescending(selector).ToList();
    }

    /// <summary>
    /// Changes the sort column (toggles direction if same column)
    /// </summary>
    /// <param name="columnName">Column identifier to sort by</param>
    public void ChangeSortColumn(string columnName)
    {
        if (SortColumn == columnName)
        {
            SortAscending = !SortAscending;
        }
        else
        {
            SortColumn = columnName;
            SortAscending = true;
        }
    }

    /// <summary>
    /// Gets the CSS class for sort indicator icon
    /// </summary>
    /// <param name="columnName">Column identifier</param>
    /// <returns>CSS class string for bi icon (bi-arrow-up or bi-arrow-down)</returns>
    public string GetSortIcon(string columnName)
    {
        if (SortColumn != columnName)
        {
            return string.Empty;
        }

        return SortAscending ? "bi-arrow-up" : "bi-arrow-down";
    }

    /// <summary>
    /// Checks if a column is currently being sorted
    /// </summary>
    /// <param name="columnName">Column identifier</param>
    /// <returns>True if this column is the active sort column</returns>
    public bool IsActiveSortColumn(string columnName)
    {
        return SortColumn == columnName;
    }

    /// <summary>
    /// Resets sorting to default state
    /// </summary>
    public void Reset()
    {
        SortColumn = string.Empty;
        SortAscending = true;
    }
}
