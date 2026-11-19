using System.Text.Json;
using System.Text.Json.Nodes;
using RTUB.Core.Constants;

namespace RTUB.Application.Services;

// Audit log: value redaction, formatting and comparison helpers.

public partial class AuditLogAppender
{
    /// <summary>Private-conversation content (AuditConfiguration.RedactedProperties) never lands in a log entry.</summary>
    private static void Redact(Dictionary<string, object?> changes, string entityType)
    {
        if (!AuditConfiguration.RedactedProperties.TryGetValue(entityType, out var redacted))
            return;

        foreach (var name in changes.Keys.Where(redacted.Contains).ToList())
            changes[name] = AuditConfiguration.RedactedValue;
    }

    /// <summary>
    /// Entries written before a property joined <see cref="AuditConfiguration.RedactedProperties"/> (push
    /// subscriptions included) still hold its value. This hides it whenever an entry is shown or exported - the stored
    /// row is never rewritten. The key stays, so the reader sees that it was set or changed. Text that is not a JSON
    /// object, or holds nothing to hide, comes back exactly as stored.
    /// </summary>
    public static string? RedactStored(string? entityType, string? changes)
    {
        if (string.IsNullOrEmpty(changes) || entityType is null
            || !AuditConfiguration.RedactedProperties.TryGetValue(entityType, out var redacted))
            return changes;

        JsonObject? stored;
        try
        {
            stored = JsonNode.Parse(changes) as JsonObject;
        }
        catch (JsonException)
        {
            return changes;
        }

        var exposed = stored?
            .Where(p => redacted.Contains(p.Key) && !(p.Value is JsonValue v && v.TryGetValue<string>(out var text) && text == AuditConfiguration.RedactedValue))
            .Select(p => p.Key)
            .ToList();
        if (stored is null || exposed is not { Count: > 0 })
            return changes;

        foreach (var name in exposed)
            stored[name] = AuditConfiguration.RedactedValue;
        return stored.ToJsonString();
    }

    // --- Private helpers ---

    /// <summary>AuditLog.EntityDisplayName holds at most 500 characters.</summary>
    private static string? Fit(string? name) => name is { Length: > 500 } ? name[..499] + "…" : name;

    /// <summary>
    /// Builds a ChangeEntry for a modified property, with special handling for binary data.
    /// </summary>
    private ChangeEntry BuildPropertyChange(string fieldName, object? oldValue, object? newValue)
    {
        if (newValue is byte[] newBytes)
        {
            var oldDescription = oldValue is byte[] oldBytes && oldBytes.Length > 0
                ? GetBinaryDataDescription(fieldName, oldBytes.Length)
                : null;
            return new ChangeEntry(oldDescription, GetBinaryDataDescription(fieldName, newBytes.Length));
        }
        return new ChangeEntry(oldValue, newValue);
    }

    private static string GetBinaryDataDescription(string? fieldName, int byteCount)
    {
        var lower = fieldName?.ToLowerInvariant() ?? string.Empty;

        var label = (lower.Contains("picture") || lower.Contains("photo") || lower.Contains("avatar")) ? "Picture" :
                    lower.Contains("image") ? "Image" :
                    (lower.Contains("file") || lower.Contains("document") || lower.Contains("pdf")) ? "File" :
                    "Binary data";

        return $"[{label} uploaded: {FormatBytes(byteCount)}]";
    }

    private static string FormatBytes(int bytes) => bytes switch
    {
        < 1024 => $"{bytes} bytes",
        < 1024 * 1024 => $"{bytes / 1024} KB",
        _ => $"{bytes / (1024 * 1024)} MB"
    };

    /// <summary>
    /// Compares two values for semantic equality, handling collections and JSON strings.
    /// </summary>
    private static bool AreValuesEqual(object? oldValue, object? newValue)
    {
        if (ReferenceEquals(oldValue, newValue)) return true;
        if (oldValue is null || newValue is null) return false;

        // Collection comparison (e.g. List<T> for primitive collections)
        if (oldValue is System.Collections.IEnumerable oldEnum &&
            newValue is System.Collections.IEnumerable newEnum &&
            oldValue is not string && newValue is not string)
        {
            var oldList = oldEnum.Cast<object>().ToList();
            var newList = newEnum.Cast<object>().ToList();
            return oldList.Count == newList.Count && oldList.SequenceEqual(newList);
        }

        // String comparison — only attempt JSON parse if both look like JSON objects/arrays
        if (oldValue is string oldStr && newValue is string newStr)
        {
            if (oldStr == newStr) return true;

            var oldIsJson = oldStr.Length > 0 && (oldStr[0] == '{' || oldStr[0] == '[');
            var newIsJson = newStr.Length > 0 && (newStr[0] == '{' || newStr[0] == '[');

            if (oldIsJson && newIsJson)
            {
                try
                {
                    var o = JsonSerializer.Deserialize<object>(oldStr);
                    var n = JsonSerializer.Deserialize<object>(newStr);
                    return JsonSerializer.Serialize(o) == JsonSerializer.Serialize(n);
                }
                catch
                {
                    // Fall through to false
                }
            }

            return false;
        }

        return Equals(oldValue, newValue);
    }
}
