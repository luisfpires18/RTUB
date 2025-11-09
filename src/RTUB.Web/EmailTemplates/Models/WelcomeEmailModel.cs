namespace RTUB.Web.EmailTemplates.Models;

/// <summary>
/// Model for welcome email template
/// </summary>
public class WelcomeEmailModel
{
    public string UserName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
