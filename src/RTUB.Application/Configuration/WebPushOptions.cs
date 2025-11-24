namespace RTUB.Application.Configuration;

/// <summary>
/// Configuration options for Web Push notifications
/// </summary>
public class WebPushOptions
{
    public const string SectionName = "WebPush";

    /// <summary>
    /// Determines whether Web Push notifications are enabled for non-OWNER users
    /// OWNER role users always have access regardless of this setting
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// VAPID subject (usually a mailto: URL or HTTPS URL)
    /// Example: mailto:admin@example.com or https://example.com
    /// </summary>
    public string VapidSubject { get; set; } = string.Empty;

    /// <summary>
    /// VAPID public key for push subscription
    /// </summary>
    public string VapidPublicKey { get; set; } = string.Empty;

    /// <summary>
    /// VAPID private key for signing push messages
    /// IMPORTANT: Keep this secret and never expose to client-side code
    /// </summary>
    public string VapidPrivateKey { get; set; } = string.Empty;

    /// <summary>
    /// Validates whether the WebPush configuration is properly set up
    /// </summary>
    public bool IsConfigured()
    {
        return !string.IsNullOrWhiteSpace(VapidSubject) &&
               !string.IsNullOrWhiteSpace(VapidPublicKey) &&
               !string.IsNullOrWhiteSpace(VapidPrivateKey);
    }
}
