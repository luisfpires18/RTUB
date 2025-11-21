namespace RTUB.Application.DTOs;

/// <summary>
/// Result DTO for member's status in the Tuna
/// Includes retirement status and last activity dates
/// </summary>
public class MemberStatusResult
{
    /// <summary>
    /// Indicates if the member is currently retired
    /// </summary>
    public bool IsRetired { get; set; }
    
    /// <summary>
    /// Date of last rehearsal where member was marked present/approved
    /// Only includes PAST rehearsals (Date < DateTime.UtcNow)
    /// Excludes canceled rehearsals
    /// </summary>
    public DateTime? LastRehearsalDate { get; set; }
    
    /// <summary>
    /// Date of last event where member was enrolled (WillAttend = true)
    /// Only includes PAST events (end date < DateTime.UtcNow)
    /// Excludes canceled events
    /// </summary>
    public DateTime? LastEventDate { get; set; }
    
    /// <summary>
    /// Maximum of LastRehearsalDate and LastEventDate
    /// Represents the most recent activity overall
    /// </summary>
    public DateTime? LastActivityDate { get; set; }
    
    /// <summary>
    /// True if the member has at least one past activity (rehearsal or event)
    /// Used to determine if the "Estado na Tuna" section should be shown
    /// </summary>
    public bool HasAnyActivity { get; set; }
}
