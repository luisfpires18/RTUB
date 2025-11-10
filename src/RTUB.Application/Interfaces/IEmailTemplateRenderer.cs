namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for rendering email templates
/// </summary>
public interface IEmailTemplateRenderer
{
    /// <summary>
    /// Renders the new request notification email
    /// </summary>
    Task<string> RenderNewRequestNotificationAsync(
        string requestName,
        string requestEmail,
        string phone,
        string eventType,
        string dateInfo,
        string location,
        string message,
        DateTime createdAt);

    /// <summary>
    /// Renders the welcome email for new members
    /// </summary>
    Task<string> RenderWelcomeEmailAsync(
        string userName,
        string fullName,
        string nickName,
        string password);

    /// <summary>
    /// Renders the event notification email
    /// </summary>
    Task<string> RenderEventNotificationAsync(
        string eventTitle,
        string dateFormatted,
        string eventLocation,
        string eventLink,
        string nickname = "");

    /// <summary>
    /// Renders the password reset email
    /// </summary>
    Task<string> RenderPasswordResetAsync(string callbackUrl);
}
