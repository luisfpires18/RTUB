namespace RTUB.Web.EmailTemplates.Models;

/// <summary>
/// Model for birthday notification email template
/// </summary>
public class BirthdayNotificationModel
{
    public string BirthdayPersonNickname { get; set; } = string.Empty;
    public string BirthdayPersonFullName { get; set; } = string.Empty;
    public string RecipientNickname { get; set; } = string.Empty;
    public string RecipientFullName { get; set; } = string.Empty;
    public string PreferencesLink { get; set; } = "https://rtub.azurewebsites.net/profile";
}
