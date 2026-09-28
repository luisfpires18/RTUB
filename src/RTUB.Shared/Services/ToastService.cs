namespace RTUB.Shared;

public enum ToastIntent
{
    Success,
    Info,
    Warning,
    Error
}

public sealed record Toast(Guid Id, ToastIntent Intent, string Message, string? Title)
{
    /// <summary>Success and info leave by themselves; warnings and errors wait for the user.</summary>
    public bool AutoDismiss => Intent is ToastIntent.Success or ToastIntent.Info;
}

/// <summary>
/// Transient feedback for the current circuit ("Notificação enviada", "Link copiado").
/// Scoped, so a page and the layout's <see cref="ToastHost"/> share it. Not for errors the user
/// must act on in place (Alert, ErrorDisplay, FormField) and never for confirmations
/// (docs/design/RTUB_UI_REFACTOR.md section 23.5).
/// </summary>
public sealed class ToastService
{
    /// <summary>Oldest toasts leave when more than this many are showing.</summary>
    public const int MaxVisible = 3;

    private readonly List<Toast> _toasts = new();
    private readonly object _gate = new();

    public event Action? Changed;

    public IReadOnlyList<Toast> Toasts
    {
        get { lock (_gate) { return _toasts.ToList(); } }
    }

    public void ShowSuccess(string message, string? title = null) => Show(ToastIntent.Success, message, title);
    public void ShowInfo(string message, string? title = null) => Show(ToastIntent.Info, message, title);
    public void ShowWarning(string message, string? title = null) => Show(ToastIntent.Warning, message, title);
    public void ShowError(string message, string? title = null) => Show(ToastIntent.Error, message, title);

    public void Show(ToastIntent intent, string message, string? title = null)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        lock (_gate)
        {
            // The same message twice (a double tap) moves to the end instead of stacking.
            _toasts.RemoveAll(t => t.Intent == intent && t.Message == message && t.Title == title);
            _toasts.Add(new Toast(Guid.NewGuid(), intent, message, title));
            if (_toasts.Count > MaxVisible)
            {
                _toasts.RemoveRange(0, _toasts.Count - MaxVisible);
            }
        }

        Changed?.Invoke();
    }

    public void Dismiss(Guid id)
    {
        bool removed;
        lock (_gate)
        {
            removed = _toasts.RemoveAll(t => t.Id == id) > 0;
        }

        if (removed)
        {
            Changed?.Invoke();
        }
    }
}
