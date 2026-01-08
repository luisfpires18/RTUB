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
