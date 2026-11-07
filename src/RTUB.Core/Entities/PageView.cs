namespace RTUB.Core.Entities;

/// <summary>
/// Task 089 (/owner/dashboard → "Páginas mais vistas"): how many times a React page was opened on one Portugal day.
/// One row per day and route (ids normalised to <c>{id}</c>, no query string), counted in memory and flushed once a
/// minute by <c>PageViewFlushService</c>: never a write per request. Path only: no member, no address, no user agent.
/// </summary>
public class PageView
{
    public int Id { get; set; }

    /// <summary>Portugal calendar day, midnight, unspecified kind.</summary>
    public DateTime Day { get; set; }

    /// <summary>A route such as "/events/{id}" (≤ 128 characters).</summary>
    public string Path { get; set; } = string.Empty;

    public int Count { get; set; }
}
