using System.Text.Json;
using System.Text.Json.Serialization;

namespace RTUB.Shared;

/// <summary>
/// "Unsaved changes" for one form: the editable values differ from the values the form started
/// with (or was last saved with). Focusing a field, validating, or typing and undoing is not a
/// change. The form says which values count by passing a snapshot of them - usually an anonymous
/// object of its editable fields, or its form model (docs/design/RTUB_UI_REFACTOR.md 24.2).
/// </summary>
/// <example>
/// editChanges.Track(() => new { editLocation, editTheme });  // when the form opens
/// &lt;Modal IsDirty="() => editChanges.IsDirty" ...&gt;
/// editChanges.Clear();                                       // after a successful save
/// </example>
public sealed class UnsavedChanges
{
    private static readonly JsonSerializerOptions Options = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        MaxDepth = 16
    };

    private Func<object?>? _snapshot;
    private string? _baseline;

    /// <summary>Starts tracking: the current values become the clean baseline.</summary>
    public void Track(Func<object?> snapshot)
    {
        _snapshot = snapshot;
        MarkClean();
    }

    /// <summary>The current values become the baseline (after a save that keeps the form open).</summary>
    public void MarkClean() => _baseline = Serialize(_snapshot?.Invoke());

    /// <summary>Stops tracking (the form closed, was saved and closed, or was discarded).</summary>
    public void Clear()
    {
        _snapshot = null;
        _baseline = null;
    }

    public bool IsTracking => _snapshot != null;

    public bool IsDirty => _snapshot != null && Serialize(_snapshot()) != _baseline;

    private static string Serialize(object? value) =>
        value == null ? "null" : JsonSerializer.Serialize(value, value.GetType(), Options);
}
