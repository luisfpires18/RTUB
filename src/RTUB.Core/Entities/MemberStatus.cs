namespace RTUB.Core.Entities;

/// <summary>
/// Calculated projection of a member's activity status (task 044B). Never business truth: every reconciliation
/// recomputes it from the attendance data; <see cref="ApplicationUser.IsRetired"/> is the authoritative flag and
/// <see cref="IsRetired"/> here is always written together with it, in the same SaveChanges.
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

    /// <summary>Copy of <see cref="ApplicationUser.IsRetired"/> as of the last reconciliation (Reformado = true).</summary>
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
    /// Retired in 044B: no longer read or written (the permanent manual override and "Tornar ativo" were removed; the
    /// activity rules alone decide the state). Kept only so the schema needs no migration; drop it in a later one.
    /// </summary>
    public bool OverrideRetired { get; set; }

    /// <summary>When the projection last changed (it is rewritten only when a value differs).</summary>
    public DateTime LastUpdatedAt { get; set; }
}
