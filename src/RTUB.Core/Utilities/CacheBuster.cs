namespace RTUB.Core.Utilities;

/// <summary>
/// Provides a cache-busting version query string that changes once per deployment / app restart.
/// Use <see cref="Bust"/> in Razor &lt;img&gt; tags to avoid stale images after releasing.
/// </summary>
public static class CacheBuster
{
    /// <summary>
    /// Version query string generated at application startup (e.g. "?v=1707580800").
    /// </summary>
    public static readonly string V = $"?v={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

    /// <summary>
    /// Appends the cache-busting version to a URL.
    /// Returns empty string for null/empty input so <c>&lt;img src=""&gt;</c> stays harmless.
    /// </summary>
    public static string Bust(string? url)
        => string.IsNullOrEmpty(url) ? "" : $"{url}{V}";
}
