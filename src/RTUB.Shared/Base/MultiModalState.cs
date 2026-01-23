namespace RTUB.Shared.Base;

/// <summary>
/// Manages state for multiple modals using a dictionary-based approach.
/// Replaces multiple boolean flags (showEditModal, showDeleteModal, etc.) with a single state manager.
/// Only one modal can be open at a time by default (closes others when opening a new one).
/// </summary>
/// <typeparam name="TKey">The type of key used to identify modals (typically an enum or string)</typeparam>
public class MultiModalState<TKey> where TKey : notnull
{
    private readonly Dictionary<TKey, bool> _states = new();

    /// <summary>
    /// Checks if a modal with the given key is currently open
    /// </summary>
    /// <param name="key">The modal key</param>
    /// <returns>True if the modal is open, false otherwise</returns>
    public bool IsOpen(TKey key)
    {
        return _states.GetValueOrDefault(key, false);
    }

    /// <summary>
    /// Opens a modal with the given key and closes all other modals
    /// </summary>
    /// <param name="key">The modal key to open</param>
    public void Open(TKey key)
    {
        CloseAll();
        _states[key] = true;
    }

    /// <summary>
    /// Closes a modal with the given key
    /// </summary>
    /// <param name="key">The modal key to close</param>
    public void Close(TKey key)
    {
        _states[key] = false;
    }

    /// <summary>
    /// Closes all modals
    /// </summary>
    public void CloseAll()
    {
        foreach (var key in _states.Keys.ToList())
        {
            _states[key] = false;
        }
    }

    /// <summary>
    /// Toggles the state of a modal (opens if closed, closes if open)
    /// </summary>
    /// <param name="key">The modal key to toggle</param>
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
    /// Sets the state of a modal based on the provided boolean value.
    /// Used for ShowChanged event handlers in Blazor components.
    /// </summary>
    /// <param name="key">The modal key</param>
    /// <param name="isOpen">True to open the modal, false to close it</param>
    public void Toggle(TKey key, bool isOpen)
    {
        if (isOpen)
        {
            Open(key);
        }
        else
        {
            Close(key);
        }
    }

    /// <summary>
    /// Gets the number of modals currently registered
    /// </summary>
    public int Count => _states.Count;

    /// <summary>
    /// Checks if any modal is currently open
    /// </summary>
    public bool AnyOpen => _states.Values.Any(v => v);
}
