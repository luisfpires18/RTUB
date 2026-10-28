namespace RTUB.Core.Entities;

/// <summary>
/// One sign-in, or one hour in which a member was seen signed in (task 050, /owner/dashboard). Written with raw SQL by
/// the sign-in endpoint and the cookie validation; read only by the Owner dashboard. Not audited.
/// </summary>
public class LoginEvent
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    /// <summary>UTC. For a "seen" row, the first request of that hour.</summary>
    public DateTime At { get; set; }

    /// <summary>True for a password sign-in; false for "seen" (at most one per member per hour).</summary>
    public bool IsSignIn { get; set; }
}
