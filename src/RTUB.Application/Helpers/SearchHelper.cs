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

    // Cache normalized search terms to avoid repeated allocations
    private string[]? _normalizedSearchTerms;
    private string? _lastSearchTerm;
    private bool _lastCaseSensitive;
    private bool _lastAccentSensitive;

    /// <summary>
    /// Filters a list of items based on the current search term
    /// </summary>
    /// <param name="items">List of items to filter</param>
    /// <param name="selector">Function to extract the searchable string from each item</param>
    /// <param name="caseSensitive">Whether the search should be case-sensitive (default: false)</param>
    /// <returns>Filtered list of items</returns>
    public List<T> Filter(List<T> items, Func<T, string> selector, bool caseSensitive = false)
    {
        if (items == null || items.Count == 0)
        {
            return new List<T>();
        }

        if (string.IsNullOrWhiteSpace(SearchTerm))
        {
            return items;
        }

        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        return items.Where(item =>
        {
            var value = selector(item);
            if (string.IsNullOrEmpty(value)) return false;

            return value.Contains(SearchTerm, comparison);
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
    /// <param name="accentSensitive">Whether the search should be accent-sensitive (default: false)</param>
    /// <returns>Filtered list of items</returns>
    public List<T> FilterMultiple(List<T> items, List<Func<T, string>> selectors, bool caseSensitive = false, bool accentSensitive = false)
    {
        if (items == null || items.Count == 0)
        {
            return new List<T>();
        }

        if (string.IsNullOrWhiteSpace(SearchTerm))
        {
            return items;
        }

        if (selectors == null || selectors.Count == 0)
        {
            return items;
        }

        // Cache normalized search terms to avoid repeated allocations
        var searchTerms = GetNormalizedSearchTerms(caseSensitive, accentSensitive);
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        return items.Where(item =>
        {
            // For each search term, check if it exists in any of the selectors
            return searchTerms.All(searchTerm =>
            {
                return selectors.Any(selector =>
                {
                    var value = selector(item);
                    if (string.IsNullOrEmpty(value)) return false;

                    var compareValue = value;
                    if (!accentSensitive)
                    {
                        compareValue = RTUB.Application.Helpers.UsernameHelper.RemoveDiacritics(compareValue);
                    }
                    return compareValue.Contains(searchTerm, comparison);
                });
            });
        }).ToList();
    }

    /// <summary>
    /// Filters a list of items where ALL search terms must match
    /// Search term is split by spaces and all parts must be found
    /// Note: This method does not normalize accents/diacritics (preserves original behavior)
    /// </summary>
    /// <param name="items">List of items to filter</param>
    /// <param name="selector">Function to extract the searchable string from each item</param>
    /// <param name="caseSensitive">Whether the search should be case-sensitive (default: false)</param>
    /// <returns>Filtered list of items</returns>
    public List<T> FilterAllTerms(List<T> items, Func<T, string> selector, bool caseSensitive = false)
    {
        if (items == null || items.Count == 0)
        {
            return new List<T>();
        }

        if (string.IsNullOrWhiteSpace(SearchTerm))
        {
            return items;
        }

        // Cache normalized search terms to avoid repeated allocations
        // accentSensitive: true means don't remove diacritics (preserves original behavior)
        var searchTerms = GetNormalizedSearchTerms(caseSensitive, accentSensitive: true);
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        return items.Where(item =>
        {
            var value = selector(item);
            if (string.IsNullOrEmpty(value)) return false;

            return searchTerms.All(term => value.Contains(term, comparison));
        }).ToList();
    }

    /// <summary>
    /// Gets normalized search terms with caching to avoid repeated allocations
    /// </summary>
    private string[] GetNormalizedSearchTerms(bool caseSensitive, bool accentSensitive)
    {
        // Return cached result if parameters match
        if (_normalizedSearchTerms != null &&
            _lastSearchTerm == SearchTerm &&
            _lastCaseSensitive == caseSensitive &&
            _lastAccentSensitive == accentSensitive)
        {
            return _normalizedSearchTerms;
        }

        // Split and normalize search terms
        var terms = SearchTerm.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (!caseSensitive)
        {
            for (int i = 0; i < terms.Length; i++)
            {
                terms[i] = terms[i].ToLowerInvariant();
            }
        }

        if (!accentSensitive)
        {
            for (int i = 0; i < terms.Length; i++)
            {
                terms[i] = RTUB.Application.Helpers.UsernameHelper.RemoveDiacritics(terms[i]);
            }
        }

        // Cache the result
        _normalizedSearchTerms = terms;
        _lastSearchTerm = SearchTerm;
        _lastCaseSensitive = caseSensitive;
        _lastAccentSensitive = accentSensitive;

        return terms;
    }

    /// <summary>
    /// Clears the current search term and resets cache
    /// </summary>
    public void Clear()
    {
        SearchTerm = string.Empty;
        _normalizedSearchTerms = null;
        _lastSearchTerm = null;
        _lastCaseSensitive = false;
        _lastAccentSensitive = false;
    }

    /// <summary>
    /// Checks if a search is currently active
    /// </summary>
    public bool IsSearching => !string.IsNullOrWhiteSpace(SearchTerm);
}
