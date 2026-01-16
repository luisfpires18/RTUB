namespace RTUB.Application.Configuration;

/// <summary>
/// Configuration options for Web Push notifications
/// </summary>
public class WebPushOptions
{
    public const string SectionName = "WebPush";
    private const string DefaultBaseUrl = "https://localhost";

    /// <summary>
    /// Determines whether Web Push notifications are enabled for non-OWNER users
    /// OWNER role users always have access regardless of this setting
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Base URL of the application for constructing absolute notification URLs
    /// Example: https://app.rtub.pt
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

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

    /// <summary>
    /// Gets the base URL for building absolute notification URLs.
    /// Returns the configured BaseUrl, or a default if not configured.
    /// </summary>
    public string GetEffectiveBaseUrl()
    {
        return string.IsNullOrWhiteSpace(BaseUrl) ? DefaultBaseUrl : BaseUrl.TrimEnd('/');
    }
}
