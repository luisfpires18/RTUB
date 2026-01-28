namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for audit log display operations (badge styling, formatting)
/// Extracted from AuditLog.razor to improve separation of concerns
/// </summary>
public interface IAuditLogDisplayService
{
    /// <summary>
    /// Gets the CSS class for an action badge
    /// </summary>
    /// <param name="action">The action name</param>
    /// <returns>Bootstrap badge class</returns>
    string GetActionBadgeClass(string action);

    /// <summary>
    /// Formats changes JSON for display
    /// </summary>
    /// <param name="changes">JSON string of changes</param>
    /// <returns>Formatted JSON string or error message</returns>
    string FormatChanges(string? changes);

    /// <summary>
    /// Exports audit logs to JSON format for download
    /// </summary>
    /// <param name="logs">Collection of audit logs to export</param>
    /// <returns>JSON string</returns>
    string ExportToJson(IEnumerable<object> logs);
}
