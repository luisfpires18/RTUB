namespace RTUB.Core.Entities;

/// <summary>
/// Represents a Web Push notification subscription for a user
/// Stores the push subscription details required for sending notifications
/// </summary>
public class PushSubscription : BaseEntity
{
    /// <summary>
    /// User ID that owns this subscription
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Navigation property to the user
    /// </summary>
    public ApplicationUser? User { get; set; }

    /// <summary>
    /// Push subscription endpoint URL
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// P256DH key for encryption
    /// </summary>
    public string P256dh { get; set; } = string.Empty;

    /// <summary>
    /// Auth secret for encryption
    /// </summary>
    public string Auth { get; set; } = string.Empty;

    /// <summary>
    /// Optional user agent information
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Date when the subscription expires (if known)
    /// </summary>
    public DateTime? ExpirationTime { get; set; }
}
