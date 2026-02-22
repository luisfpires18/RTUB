using System.Text.Json;

namespace RTUB.Application.Helpers;

/// <summary>
/// Shared, reusable JsonSerializerOptions instances.
/// Creating a new JsonSerializerOptions per call is expensive — cache and reuse.
/// </summary>
public static class JsonSerializerConstants
{
    /// <summary>
    /// Compact JSON (no indentation). Used for combat replays, API payloads, etc.
    /// </summary>
    public static readonly JsonSerializerOptions Compact = new()
    {
        WriteIndented = false
    };

    /// <summary>
    /// Indented JSON for human-readable output. Used for audit log display/export.
    /// </summary>
    public static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true
    };
}
