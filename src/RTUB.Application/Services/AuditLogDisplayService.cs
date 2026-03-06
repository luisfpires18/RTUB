using System.Text.Json;
using Microsoft.Extensions.Logging;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// Service for audit log display operations (badge styling, formatting)
/// Extracted from AuditLog.razor to improve separation of concerns
/// </summary>
public class AuditLogDisplayService : IAuditLogDisplayService
{
    private readonly ILogger<AuditLogDisplayService> _logger;

    public AuditLogDisplayService(ILogger<AuditLogDisplayService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets the CSS class for an action badge
    /// </summary>
    /// <param name="action">The action name</param>
    /// <returns>Bootstrap badge class</returns>
    public string GetActionBadgeClass(string action)
    {
        return action switch
        {
            "Created" => "bg-success",
            "Modified" => "bg-warning text-dark",
            "Deleted" => "bg-danger",
            "Role Added" => "bg-primary",
            "Role Removed" => "bg-danger",
            _ => "bg-info"
        };
    }

    /// <summary>
    /// Formats changes JSON for display
    /// </summary>
    /// <param name="changes">JSON string of changes</param>
    /// <returns>Formatted JSON string or error message</returns>
    public string FormatChanges(string? changes)
    {
        if (string.IsNullOrEmpty(changes))
            return "No changes recorded";

        try
        {
            var formatted = JsonSerializer.Serialize(
                JsonSerializer.Deserialize<object>(changes),
                JsonSerializerConstants.Indented
            );
            return formatted;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to format JSON changes for audit log display");
            return changes;
        }
    }

    /// <summary>
    /// Exports audit logs to JSON format for download
    /// </summary>
    /// <param name="logs">Collection of audit logs to export</param>
    /// <returns>JSON string</returns>
    public string ExportToJson(IEnumerable<object> logs)
    {
        return JsonSerializer.Serialize(logs, JsonSerializerConstants.Indented);
    }
}
