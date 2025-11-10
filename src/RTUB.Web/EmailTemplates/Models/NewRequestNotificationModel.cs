namespace RTUB.Web.EmailTemplates.Models;

/// <summary>
/// Model for new request notification email template
/// </summary>
public class NewRequestNotificationModel
{
    public string RequestName { get; set; } = string.Empty;
    public string RequestEmail { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string DateInfo { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
