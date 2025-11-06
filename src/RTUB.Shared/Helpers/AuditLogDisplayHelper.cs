using System.Text.Json;

namespace RTUB.Shared.Helpers;

/// <summary>
/// Helper class for rendering audit log entries with special handling for ApplicationUser login events
/// </summary>
public class AuditLogDisplayHelper
{
    /// <summary>
    /// Represents the display information for an audit log entry
    /// </summary>
    public class DisplayInfo
    {
        public string DisplayAction { get; set; } = string.Empty;
        public bool ShowLoggedInBadge { get; set; }
        public string? TargetUser { get; set; }
    }

    /// <summary>
    /// Determines the display action and additional badges for an audit log entry
    /// </summary>
    public static DisplayInfo GetDisplayInfo(string entityType, string action, string? changes)
    {
        var result = new DisplayInfo
        {
            DisplayAction = action,
            ShowLoggedInBadge = false
        };

        // Only process ApplicationUser Modified actions
        if (entityType != "ApplicationUser" || action != "Modified" || string.IsNullOrEmpty(changes))
        {
            return result;
        }

        try
        {
            var changesObj = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(changes);
            if (changesObj == null)
            {
                return result;
            }

            // Extract target user if present
            if (changesObj.ContainsKey("_TargetUser") && changesObj["_TargetUser"].ValueKind == JsonValueKind.String)
            {
                result.TargetUser = changesObj["_TargetUser"].GetString();
            }

            // Check for LastLoginDate change
            if (!changesObj.ContainsKey("LastLoginDate"))
            {
                return result;
            }

            var lastLoginDateElement = changesObj["LastLoginDate"];
            
            // Parse Old and New values
            DateTime? oldDate = ParseDateTime(lastLoginDateElement, "Old");
            DateTime? newDate = ParseDateTime(lastLoginDateElement, "New");

            // Determine if this is a login event
            bool isLoginEvent = false;
            
            if (newDate.HasValue)
            {
                if (!oldDate.HasValue || newDate.Value > oldDate.Value)
                {
                    isLoginEvent = true;
                }
            }

            if (!isLoginEvent)
            {
                return result;
            }

            // Count non-metadata fields that changed (excluding fields starting with _)
            var nonMetadataChanges = changesObj.Keys.Where(k => !k.StartsWith("_")).ToList();

            // If only LastLoginDate changed, display as "Logged in"
            if (nonMetadataChanges.Count == 1 && nonMetadataChanges[0] == "LastLoginDate")
            {
                result.DisplayAction = "Logged in";
            }
            else
            {
                // Mixed changes: keep "Profile Updated" or original action but show badge
                result.DisplayAction = "Profile Updated";
                result.ShowLoggedInBadge = true;
            }
        }
        catch (JsonException)
        {
            // If parsing fails, return original action
        }

        return result;
    }

    /// <summary>
    /// Parses a DateTime from a JSON element's property, handling various formats
    /// </summary>
    private static DateTime? ParseDateTime(JsonElement element, string propertyName)
    {
        try
        {
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.String)
                {
                    var dateStr = prop.GetString();
                    if (string.IsNullOrEmpty(dateStr))
                    {
                        return null;
                    }

                    // Try parsing as ISO 8601 (with or without Z)
                    if (DateTime.TryParse(dateStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var date))
                    {
                        return date.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(date, DateTimeKind.Utc) : date.ToUniversalTime();
                    }
                }
                else if (prop.ValueKind == JsonValueKind.Null)
                {
                    return null;
                }
            }
            else if (element.ValueKind == JsonValueKind.String)
            {
                // Handle case where the value itself is a string (for "New" when "Old" is null)
                var dateStr = element.GetString();
                if (!string.IsNullOrEmpty(dateStr) && DateTime.TryParse(dateStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var date))
                {
                    return date.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(date, DateTimeKind.Utc) : date.ToUniversalTime();
                }
            }
        }
        catch
        {
            // Parsing failed
        }

        return null;
    }

    /// <summary>
    /// Applies debouncing logic to a list of audit logs, collapsing duplicate login events within 2 minutes
    /// </summary>
    public static List<T> ApplyDebouncing<T>(List<T> logs, Func<T, string> getEntityType, 
        Func<T, string> getAction, Func<T, string?> getChanges, Func<T, DateTime> getTimestamp,
        Func<T, string?> getUserName) where T : class
    {
        var result = new List<T>();
        var seenLogins = new Dictionary<string, (T log, DateTime timestamp)>();

        foreach (var log in logs)
        {
            var entityType = getEntityType(log);
            var action = getAction(log);
            var changes = getChanges(log);
            var timestamp = getTimestamp(log);
            var userName = getUserName(log);

            var displayInfo = GetDisplayInfo(entityType, action, changes);
            
            // Check if this is a "Logged in" event
            if (displayInfo.DisplayAction == "Logged in" || displayInfo.ShowLoggedInBadge)
            {
                var targetUser = displayInfo.TargetUser ?? userName ?? "Unknown";

                if (seenLogins.ContainsKey(targetUser))
                {
                    var (existingLog, existingTimestamp) = seenLogins[targetUser];
                    var timeDiff = Math.Abs((timestamp - existingTimestamp).TotalMinutes);

                    if (timeDiff <= 2)
                    {
                        // Skip this entry (debounced), but we could update the note on the existing one
                        // For now, we simply skip it as per requirements
                        continue;
                    }
                }

                // Update the seen logins tracker
                seenLogins[targetUser] = (log, timestamp);
            }

            result.Add(log);
        }

        return result;
    }
}
