using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a login count record
/// Tracks when and how many times a user logged in on a specific date
/// </summary>
public class LoginCount : BaseEntity
{
    [Required]
    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// The date (without time) for which this login count applies
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Number of logins for this user on this specific date
    /// </summary>
    public int Count { get; set; } = 1;

    // Navigation property
    public virtual ApplicationUser? User { get; set; }
}
