namespace RTUB.Core.Helpers;

/// <summary>
/// Helper for parsing and simplifying User-Agent strings.
/// </summary>
public static class UserAgentHelper
{
    /// <summary>
    /// Returns a short, human-readable device/platform label from a raw User-Agent string.
    /// Examples: "Android", "iPhone", "Windows", "macOS".
    /// </summary>
    public static string GetShortUserAgent(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
            return "—";

        var ua = userAgent.ToLowerInvariant();

        if (ua.Contains("android"))
            return "Android";
        if (ua.Contains("iphone"))
            return "iPhone";
        if (ua.Contains("ipad"))
            return "iPad";
        if (ua.Contains("mobile"))
            return "Mobile";
        if (ua.Contains("windows"))
            return "Windows";
        if (ua.Contains("macintosh") || ua.Contains("mac os"))
            return "macOS";
        if (ua.Contains("linux"))
            return "Linux";

        // Truncate if too long
        return userAgent.Length > 30 ? userAgent[..30] + "…" : userAgent;
    }
}
