namespace RTUB.Web.Services;

/// <summary>
/// Singleton service that broadcasts admin-triggered refresh events
/// to all active Blazor Server circuits (e.g. after a DB-level balance reset).
/// Triggered in-process from admin pages only. It deliberately has no HTTP endpoint:
/// a cookie-authenticated POST with no body is CSRF-reachable, and nothing ever called it.
/// </summary>
public class AdminRefreshService
{
    /// <summary>
    /// Fired when an admin triggers a global data refresh.
    /// Subscribers (MainLayout) should reload user data from DB and call StateHasChanged.
    /// </summary>
    public event Func<Task>? OnRefreshRequested;

    /// <summary>
    /// Triggers a refresh across all subscribed circuits.
    /// </summary>
    public async Task TriggerRefreshAsync()
    {
        if (OnRefreshRequested is not null)
        {
            // Invoke all subscribers (each Blazor circuit) concurrently
            var delegates = OnRefreshRequested.GetInvocationList();
            var tasks = new List<Task>(delegates.Length);
            foreach (var d in delegates)
            {
                if (d is Func<Task> handler)
                {
                    tasks.Add(handler());
                }
            }
            await Task.WhenAll(tasks);
        }
    }
}
