namespace RTUB.Core.Entities;

/// <summary>
/// Stores the computed status of a member in the Tuna
/// This entity persists calculated retirement status and activity tracking to avoid repeated computations
/// Updated periodically by a background service and on-demand when activities change
/// </summary>
public class MemberStatus : BaseEntity
{
    /// <summary>
    /// User ID (Foreign key to ApplicationUser)
    /// </summary>
    public string UserId { get; set; } = null!;
    
    /// <summary>
    /// Navigation property to the user
    /// </summary>
    public ApplicationUser User { get; set; } = null!;
    
    /// <summary>
    /// Indicates if the member is currently retired
    /// Retired: 6+ months without activity
    /// Active: Less than 6 months since last activity OR has 3 consecutive months with activity
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
    
    /// <summary>
    /// For active members: Months until they become reformed (6 months from last activity)
    /// For reformed members: Consecutive months of activity completed (out of 3 needed to return to active)
    /// Null if not applicable
    /// </summary>
    public int? ProgressMonths { get; set; }
    
    /// <summary>
    /// Total months needed for progress
    /// For active members: 6 (months until reform)
    /// For reformed members: 3 (months needed to return to active)
    /// </summary>
    public int? ProgressTotalMonths { get; set; }
    
    /// <summary>
    /// Description of the progress tracking
    /// For active members: "X meses até reforma"
    /// For reformed members: "X/3 meses de atividade consecutiva"
    /// </summary>
    public string? ProgressDescription { get; set; }
    
    /// <summary>
    /// Total number of activities (rehearsals + events) the member has attended
    /// Used for sorting members by participation level
    /// </summary>
    public int TotalActivitiesCount { get; set; }
    
    /// <summary>
    /// Indicates if the retirement status was manually overridden by an administrator
    /// When true, automatic retirement calculations will not change the IsRetired status
    /// This allows admins to manually activate members without automatic re-retirement
    /// </summary>
    public bool OverrideRetired { get; set; }
    
    /// <summary>
    /// Timestamp when this status was last calculated and updated
    /// Used to determine when a refresh is needed
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }
}
