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
}
