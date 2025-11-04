namespace RTUB.Application.Helpers;

/// <summary>
/// Generic helper class for pagination logic
/// Provides reusable methods for calculating pages and getting paginated data
/// </summary>
/// <typeparam name="T">The type of items to paginate</typeparam>
public class PaginationHelper<T>
{
    /// <summary>
    /// Current page number (1-based)
    /// </summary>
    public int CurrentPage { get; set; } = 1;

    /// <summary>
    /// Number of items per page
    /// </summary>
    public int PageSize { get; set; } = 50;

    /// <summary>
    /// Total number of pages calculated from total items
    /// </summary>
    public int TotalPages { get; private set; } = 1;

    /// <summary>
    /// Total number of items
    /// </summary>
    public int TotalItems { get; private set; }

    /// <summary>
    /// Gets a paginated subset of data based on current page and page size
    /// </summary>
    /// <param name="data">Full list of data to paginate</param>
    /// <returns>Paginated subset of data</returns>
    public List<T> GetPageData(List<T> data)
    {
        if (data == null || !data.Any())
        {
            TotalItems = 0;
            TotalPages = 1;
            CurrentPage = 1;
            return new List<T>();
        }

        TotalItems = data.Count;
        TotalPages = (int)Math.Ceiling((double)TotalItems / PageSize);

        // Ensure current page is within valid range
        if (CurrentPage < 1) CurrentPage = 1;
        if (CurrentPage > TotalPages) CurrentPage = TotalPages;

        return data
            .Skip((CurrentPage - 1) * PageSize)
            .Take(PageSize)
            .ToList();
    }

    /// <summary>
    /// Navigates to the next page if available
    /// </summary>
    /// <returns>True if page was changed, false if already on last page</returns>
    public bool NextPage()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Navigates to the previous page if available
    /// </summary>
    /// <returns>True if page was changed, false if already on first page</returns>
    public bool PreviousPage()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Navigates to a specific page number
    /// </summary>
    /// <param name="page">Page number to navigate to (1-based)</param>
    /// <returns>True if page was changed, false if page number is invalid</returns>
    public bool GoToPage(int page)
    {
        if (page >= 1 && page <= TotalPages)
        {
            CurrentPage = page;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Checks if there is a next page available
    /// </summary>
    public bool HasNextPage => CurrentPage < TotalPages;

    /// <summary>
    /// Checks if there is a previous page available
    /// </summary>
    public bool HasPreviousPage => CurrentPage > 1;

    /// <summary>
    /// Gets the starting item number for the current page (1-based)
    /// </summary>
    public int StartItemNumber => TotalItems == 0 ? 0 : ((CurrentPage - 1) * PageSize) + 1;

    /// <summary>
    /// Gets the ending item number for the current page
    /// </summary>
    public int EndItemNumber => Math.Min(CurrentPage * PageSize, TotalItems);

    /// <summary>
    /// Resets pagination to first page
    /// </summary>
    public void Reset()
    {
        CurrentPage = 1;
        TotalPages = 1;
        TotalItems = 0;
    }
}
