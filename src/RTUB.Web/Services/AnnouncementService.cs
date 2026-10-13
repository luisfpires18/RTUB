namespace RTUB.Web.Services;

/// <summary>
/// The Owner's system announcement (/owner/tracing → POST /api/owner/tracing/announcement). Kept in memory only, never
/// stored, as before: the React shell polls GET /api/announcement and shows the latest one to whoever has a page open
/// within <see cref="Lifetime"/> of the send (task 037; the Blazor banner pushed it to open circuits instead).
/// One app instance holds it: a scaled-out App Service would need a shared store.
/// </summary>
public class AnnouncementService(TimeProvider? time = null)
{
    /// <summary>How long a sent announcement is still handed out.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private readonly TimeProvider _time = time ?? TimeProvider.System;

    /// <summary>The announcement shown: its id (the send time, Unix milliseconds: safe as a JS number) tells the page whether it was already seen.</summary>
    public sealed record Announcement(long Id, string Message);

    private sealed record Sent(Announcement Announcement, DateTimeOffset At);
    private Sent? _sent; // one reference, swapped whole: readers never see half an update

    /// <summary>Replaces the current announcement with <paramref name="message"/>.</summary>
    public void Broadcast(string message)
    {
        var now = _time.GetUtcNow();
        _sent = new Sent(new Announcement(now.ToUnixTimeMilliseconds(), message), now);
    }

    /// <summary>The latest announcement while it is younger than <see cref="Lifetime"/>, otherwise null.</summary>
    public Announcement? Current => _sent is { } sent && _time.GetUtcNow() - sent.At < Lifetime ? sent.Announcement : null;
}
