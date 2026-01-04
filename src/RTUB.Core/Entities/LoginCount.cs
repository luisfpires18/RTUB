using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Tracks the number of logins per user per day
/// Allows historical tracking of login frequency
/// </summary>
public class LoginCount : BaseEntity
{
    /// <summary>
    /// The user who logged in
    /// </summary>
    [Required]
    [MaxLength(450)]
    public string UserId { get; set; } = null!;

    /// <summary>
    /// Navigation property to the user
    /// </summary>
    public ApplicationUser? User { get; set; }

    /// <summary>
    /// The date of the logins (date only, no time component)
    /// </summary>
    [Required]
    public DateTime LoginDate { get; set; }

    /// <summary>
    /// The number of times the user logged in on this date
    /// </summary>
    [Required]
    public int Count { get; set; } = 0;
}
