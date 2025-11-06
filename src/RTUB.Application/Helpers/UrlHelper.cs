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
        
        // URL must be relative (start with / but not //)
        // This prevents redirects to external sites like //evil.com or http://evil.com
        return url.StartsWith("/") && !url.StartsWith("//") && !url.Contains(":");
    }
}
