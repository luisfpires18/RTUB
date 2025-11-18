namespace RTUB.Web.EmailTemplates.Models;

/// <summary>
/// Model for username changed email template
/// </summary>
public class UsernameChangedEmailModel
{
    public string FullName { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public string OldUsername { get; set; } = string.Empty;
    public string NewUsername { get; set; } = string.Empty;
    public string LoginUrl { get; set; } = "https://rtub.azurewebsites.net/login";
}
