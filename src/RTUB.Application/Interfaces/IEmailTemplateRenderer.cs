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
        string nickname = "",
        string fullName = "");

    /// <summary>
    /// Renders the password reset email
    /// </summary>
    Task<string> RenderPasswordResetAsync(string callbackUrl);

    /// <summary>
    /// Renders the birthday notification email
    /// </summary>
    Task<string> RenderBirthdayNotificationAsync(
        string birthdayPersonNickname,
        string birthdayPersonFullName,
        string recipientNickname = "",
        string recipientFullName = "");
    
    /// <summary>
    /// Renders the event cancellation notification email
    /// </summary>
    Task<string> RenderEventCancellationNotificationAsync(
        string eventTitle,
        string dateFormatted,
        string eventLocation,
        string cancellationReason,
        string eventLink,
        string nickname = "",
        string fullName = "");
    
    /// <summary>
    /// Renders the event reminder notification email
    /// </summary>
    Task<string> RenderEventReminderNotificationAsync(
        string eventTitle,
        string dateFormatted,
        string eventLocation,
        string eventLink,
        int daysUntilEvent,
        string nickname = "",
        string fullName = "",
        string eventDescription = "");
    
    /// <summary>
    /// Renders the meeting notification email
    /// </summary>
    Task<string> RenderMeetingNotificationAsync(
        string meetingType,
        string meetingTitle,
        string dateFormatted,
        string location,
        string statement,
        string senderNickname,
        string senderCity,
        string nickname = "",
        string fullName = "");
}
