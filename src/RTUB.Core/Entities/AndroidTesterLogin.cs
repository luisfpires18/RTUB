using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Tracks daily login activity for Android testers.
/// Records the first login of each day for users with IsAndroidTester = true.
/// </summary>
public class AndroidTesterLogin : BaseEntity
{
    /// <summary>
    /// The ID of the user who logged in.
    /// </summary>
    [Required]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Navigation property to the ApplicationUser.
    /// </summary>
    public ApplicationUser? User { get; set; }

    /// <summary>
    /// The date of the login (date only, no time component).
    /// Stores the UTC date when the user logged in.
    /// </summary>
    [Required]
    public DateTime LoginDate { get; set; }
}
