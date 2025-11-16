namespace RTUB.Web.EmailTemplates.Models;

/// <summary>
/// Model for meeting cancellation notification email template
/// </summary>
public class MeetingCancellationModel
{
    public string MeetingType { get; set; } = string.Empty;
    public string MeetingTitle { get; set; } = string.Empty;
    public string DateFormatted { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string CancellationReason { get; set; } = string.Empty;
    public string SenderNickname { get; set; } = string.Empty;
    public string SenderCity { get; set; } = string.Empty;
    public string? SenderPosition { get; set; }
    public string PreferencesLink { get; set; } = "https://rtub.azurewebsites.net/profile";
    public string Nickname { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}
