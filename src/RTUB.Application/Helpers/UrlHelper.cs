namespace RTUB.Application.Helpers;

/// <summary>
/// Helper class for URL validation and manipulation
/// </summary>
public static class UrlHelper
{
    /// <summary>
    /// Validates that a URL is local to the application to prevent open redirect attacks.
    /// </summary>
    /// <param name="url">The URL to validate</param>
    /// <returns>True if the URL is a valid local relative URL, false otherwise</returns>
    public static bool IsLocalUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return false;
        }
        
        // Must start with a single forward slash
        if (!url.StartsWith("/") || url.StartsWith("//"))
        {
            return false;
        }
        
        // Try to parse as relative URI
        if (!Uri.TryCreate(url, UriKind.Relative, out var uri))
        {
            return false;
        }
        
        // Ensure it doesn't contain scheme indicators that could be exploited
        // This prevents URLs like "/\evil.com" or "/:@evil.com"
        return !url.Contains("://") && !url.StartsWith("/\\");
    }
}
