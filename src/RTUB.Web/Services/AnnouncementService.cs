namespace RTUB.Web.Services;

/// <summary>
/// Singleton service that broadcasts system announcements to all active Blazor circuits.
///
/// Because this is a singleton shared across every connected user's circuit, it uses the
/// same GetInvocationList() + Task.WhenAll() pattern as MessagesNotificationService to
/// ensure every subscriber is awaited — the default multicast delegate only returns the
/// last subscriber's Task.
/// </summary>
public class AnnouncementService
{
    /// <summary>
    /// Fired when the owner broadcasts an announcement. Subscribers should call
    /// InvokeAsync(StateHasChanged) to update their UI on the correct circuit thread.
    /// </summary>
    public event Func<string, Task>? OnAnnouncement;

    /// <summary>
    /// Broadcasts a system announcement to all subscribed components.
    /// </summary>
    /// <param name="message">The announcement text to display.</param>
    public async Task BroadcastAsync(string message)
    {
        var handler = OnAnnouncement;
        if (handler is null) return;

        var delegates = handler.GetInvocationList();
        var tasks = new Task[delegates.Length];

        for (var i = 0; i < delegates.Length; i++)
        {
            try
            {
                tasks[i] = ((Func<string, Task>)delegates[i])(message);
            }
            catch
            {
                tasks[i] = Task.CompletedTask;
            }
        }

        try
        {
            await Task.WhenAll(tasks);
        }
        catch
        {
            // Individual circuit failures must not prevent other circuits from updating
        }
    }
}
