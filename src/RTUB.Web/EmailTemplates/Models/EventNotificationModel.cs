namespace RTUB.Web.EmailTemplates.Models;

/// <summary>
/// Model for event notification email template
/// </summary>
public class EventNotificationModel
{
    public string EventTitle { get; set; } = string.Empty;
    public string DateFormatted { get; set; } = string.Empty;
    public string EventLocation { get; set; } = string.Empty;
    public string EventLink { get; set; } = string.Empty;
    public string EventDescription { get; set; } = string.Empty;
    public string PreferencesLink { get; set; } = "https://rtub.azurewebsites.net/profile";
    public string Nickname { get; set; } = string.Empty;
}
