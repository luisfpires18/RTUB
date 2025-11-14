namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for handling user mentions in posts and comments
/// </summary>
public interface IMentionService
{
    /// <summary>
    /// Parse text for @mentions and resolve to user IDs
    /// </summary>
    Task<string?> ParseAndResolveAsync(string text);
    
    /// <summary>
    /// Get user suggestions for mention autocomplete
    /// </summary>
    Task<IEnumerable<(string userId, string username, string displayName)>> GetSuggestionsAsync(string query, int maxResults = 10);
    
    /// <summary>
    /// Get display names from mentions JSON
    /// </summary>
    Task<Dictionary<string, string>> GetDisplayNamesAsync(string? mentionsJson);
}
