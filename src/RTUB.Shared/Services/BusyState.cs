namespace RTUB.Shared;

/// <summary>
/// One in-flight mutation per action (docs/design/RTUB_UI_REFACTOR.md 24.7): a second
/// activation while the first runs is ignored, and the flag always resets, on success and on
/// failure. Bind the action's button to <see cref="IsBusy"/> (disabled + spinner).
/// </summary>
/// <example>
/// private readonly BusyState saving = new();
/// private Task SaveEdit() => saving.RunAsync(async () => { ... });
/// &lt;button disabled="@saving.IsBusy" ...&gt;
/// </example>
public sealed class BusyState
{
    public bool IsBusy { get; private set; }

    /// <summary>Runs the action unless one is already running. Returns false when skipped.</summary>
    public async Task<bool> RunAsync(Func<Task> action)
    {
        if (IsBusy)
        {
            return false;
        }

        IsBusy = true;
        try
        {
            await action();
            return true;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
