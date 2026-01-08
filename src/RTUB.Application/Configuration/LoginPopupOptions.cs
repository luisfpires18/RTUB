namespace RTUB.Application.Configuration;

/// <summary>
/// Configuration options for the Login Popup feature
/// </summary>
public class LoginPopupOptions
{
    public const string SectionName = "LoginPopup";

    /// <summary>
    /// Determines whether to show the popup on login
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// When true, the popup will only be shown once per user (stored in localStorage).
    /// When false, the popup will appear on every login while Enabled is true.
    /// </summary>
    public bool ReadOnce { get; set; } = true;

    /// <summary>
    /// Version identifier for the popup content. Change this value to reset the "read" status
    /// and show the popup again to all users (e.g., "v1", "v2", "patch-2024-01").
    /// Only used when ReadOnce is true.
    /// </summary>
    public string Version { get; set; } = "v1";

    /// <summary>
    /// The popup title
    /// </summary>
    public string Title { get; set; } = "Bem-vindo!";

    /// <summary>
    /// The popup description/message
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The button text to dismiss the popup
    /// </summary>
    public string ButtonText { get; set; } = "Entendi";
}
