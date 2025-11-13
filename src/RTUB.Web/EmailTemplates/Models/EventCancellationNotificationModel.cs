namespace RTUB.Web.EmailTemplates.Models;

/// <summary>
/// Model for event cancellation notification email template
/// </summary>
public class EventCancellationNotificationModel
{
    public string EventTitle { get; set; } = string.Empty;
    public string DateFormatted { get; set; } = string.Empty;
    public string EventLocation { get; set; } = string.Empty;
    public string CancellationReason { get; set; } = string.Empty;
    public string EventLink { get; set; } = string.Empty;
    public string PreferencesLink { get; set; } = "https://rtub.azurewebsites.net/profile";
    public string Nickname { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}
