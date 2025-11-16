namespace RTUB.Web.EmailTemplates.Models;

/// <summary>
/// Model for announcement email template
/// </summary>
public class AnnouncementEmailModel
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string RecipientNickname { get; set; } = string.Empty;
    public string RecipientFullName { get; set; } = string.Empty;
    public string PreferencesLink { get; set; } = "https://rtub.azurewebsites.net/profile";
}
