namespace RTUB.Shared;

/// <summary>
/// Manages the state of multiple modal dialogs, ensuring only one modal is open at a time.
/// This class provides a centralized way to handle modal visibility in Blazor pages,
/// replacing the need for multiple boolean flags per page.
/// </summary>
/// <typeparam name="TKey">The type used to identify modals (typically an enum).</typeparam>
/// <example>
/// <code>
/// private enum Modal { Edit, Delete, ViewDetails }
/// private MultiModalState&lt;Modal&gt; modals = new();
/// 
/// private void OpenEditModal(User user) {
///     editingUser = user;
///     modals.Open(Modal.Edit);
/// }
/// 
/// // In template
/// // &lt;Modal Show="@modals.IsOpen(Modal.Edit)" ...&gt;
/// </code>
/// </example>
public class MultiModalState<TKey> where TKey : notnull
{
    private readonly Dictionary<TKey, bool> _states = new();

    /// <summary>
    /// Event raised when any modal state changes. Subscribe to this event
    /// when state changes need to trigger Blazor re-renders via StateHasChanged().
    /// </summary>
    public event Action? OnStateChanged;

    /// <summary>
    /// Checks if a specific modal is currently open.
    /// </summary>
    /// <param name="key">The modal identifier.</param>
    /// <returns>True if the modal is open; otherwise, false.</returns>
    public bool IsOpen(TKey key) => _states.GetValueOrDefault(key, false);

    /// <summary>
    /// Opens a specific modal, closing all other modals first.
    /// Only one modal can be open at a time.
    /// </summary>
    /// <param name="key">The modal identifier to open.</param>
    public void Open(TKey key)
    {
        CloseAll(suppressNotification: true);
        _states[key] = true;
        NotifyStateChanged();
    }

    /// <summary>
    /// Closes a specific modal.
    /// </summary>
    /// <param name="key">The modal identifier to close.</param>
    public void Close(TKey key)
    {
        _states[key] = false;
        NotifyStateChanged();
    }

    /// <summary>
    /// Closes all open modals.
    /// </summary>
    public void CloseAll()
    {
        CloseAll(suppressNotification: false);
    }

    /// <summary>
    /// Toggles the state of a specific modal.
    /// If the modal is open, it will be closed. If closed, it will be opened
    /// (closing all other modals first).
    /// </summary>
    /// <param name="key">The modal identifier to toggle.</param>
    public void Toggle(TKey key)
    {
        if (IsOpen(key))
        {
            Close(key);
        }
        else
        {
            Open(key);
        }
    }

    /// <summary>
    /// Internal method to close all modals with optional notification suppression.
    /// </summary>
    /// <param name="suppressNotification">If true, does not raise the OnStateChanged event.</param>
    private void CloseAll(bool suppressNotification)
    {
        foreach (var key in _states.Keys.ToList())
        {
            _states[key] = false;
        }

        if (!suppressNotification)
        {
            NotifyStateChanged();
        }
    }

    /// <summary>
    /// Raises the OnStateChanged event to notify subscribers of state changes.
    /// </summary>
    private void NotifyStateChanged() => OnStateChanged?.Invoke();
}
