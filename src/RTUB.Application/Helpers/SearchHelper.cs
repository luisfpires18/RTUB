namespace RTUB.Application.Helpers;

/// <summary>
/// Generic helper class for search/filter functionality
/// Provides reusable methods for filtering lists based on search terms
/// </summary>
/// <typeparam name="T">The type of items to filter</typeparam>
public class SearchHelper<T>
{
    /// <summary>
    /// Current search term
    /// </summary>
    public string SearchTerm { get; set; } = string.Empty;

    /// <summary>
    /// Filters a list of items based on the current search term
    /// </summary>
    /// <param name="items">List of items to filter</param>
    /// <param name="selector">Function to extract the searchable string from each item</param>
    /// <param name="caseSensitive">Whether the search should be case-sensitive (default: false)</param>
    /// <returns>Filtered list of items</returns>
    public List<T> Filter(List<T> items, Func<T, string> selector, bool caseSensitive = false)
    {
        if (items == null || !items.Any())
        {
            return new List<T>();
        }

        if (string.IsNullOrWhiteSpace(SearchTerm))
        {
            return items;
        }

        var searchTerm = caseSensitive ? SearchTerm : SearchTerm.ToLower();

        return items.Where(item =>
        {
            var value = selector(item);
            if (string.IsNullOrEmpty(value)) return false;

            var compareValue = caseSensitive ? value : value.ToLower();
            return compareValue.Contains(searchTerm);
        }).ToList();
    }

    /// <summary>
    /// Filters a list of items based on multiple search criteria
    /// Returns items that match ANY of the provided selectors
    /// Each word in the search term must be found in at least one selector
    /// </summary>
    /// <param name="items">List of items to filter</param>
    /// <param name="selectors">Functions to extract searchable strings from each item</param>
    /// <param name="caseSensitive">Whether the search should be case-sensitive (default: false)</param>
    /// <returns>Filtered list of items</returns>
    public List<T> FilterMultiple(List<T> items, List<Func<T, string>> selectors, bool caseSensitive = false)
    {
        if (items == null || !items.Any())
        {
            return new List<T>();
        }

        if (string.IsNullOrWhiteSpace(SearchTerm))
        {
            return items;
        }

        if (selectors == null || !selectors.Any())
        {
            return items;
        }

        // Split search term into individual words
        var searchTerms = SearchTerm.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (!caseSensitive)
        {
            searchTerms = searchTerms.Select(t => t.ToLower()).ToArray();
        }

        return items.Where(item =>
        {
            // For each search term, check if it exists in any of the selectors
            return searchTerms.All(searchTerm =>
            {
                return selectors.Any(selector =>
                {
                    var value = selector(item);
                    if (string.IsNullOrEmpty(value)) return false;

                    var compareValue = caseSensitive ? value : value.ToLower();
                    return compareValue.Contains(searchTerm);
                });
            });
        }).ToList();
    }

    /// <summary>
    /// Filters a list of items where ALL search terms must match
    /// Search term is split by spaces and all parts must be found
    /// </summary>
    /// <param name="items">List of items to filter</param>
    /// <param name="selector">Function to extract the searchable string from each item</param>
    /// <param name="caseSensitive">Whether the search should be case-sensitive (default: false)</param>
    /// <returns>Filtered list of items</returns>
    public List<T> FilterAllTerms(List<T> items, Func<T, string> selector, bool caseSensitive = false)
    {
        if (items == null || !items.Any())
        {
            return new List<T>();
        }

        if (string.IsNullOrWhiteSpace(SearchTerm))
        {
            return items;
        }

        var searchTerms = SearchTerm.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (!caseSensitive)
        {
            searchTerms = searchTerms.Select(t => t.ToLower()).ToArray();
        }

        return items.Where(item =>
        {
            var value = selector(item);
            if (string.IsNullOrEmpty(value)) return false;

            var compareValue = caseSensitive ? value : value.ToLower();
            return searchTerms.All(term => compareValue.Contains(term));
        }).ToList();
    }

    /// <summary>
    /// Clears the current search term
    /// </summary>
    public void Clear()
    {
        SearchTerm = string.Empty;
    }

    /// <summary>
    /// Checks if a search is currently active
    /// </summary>
    public bool IsSearching => !string.IsNullOrWhiteSpace(SearchTerm);
}
