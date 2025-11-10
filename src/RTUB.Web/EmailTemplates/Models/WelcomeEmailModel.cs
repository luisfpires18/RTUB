namespace RTUB.Web.EmailTemplates.Models;

/// <summary>
/// Model for welcome email template
/// </summary>
public class WelcomeEmailModel
{
    public string UserName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string DashboardUrl { get; set; } = "https://rtub.azurewebsites.net/";
    public string ProfileUrl { get; set; } = "https://rtub.azurewebsites.net/profile";
    public string EventsUrl { get; set; } = "https://rtub.azurewebsites.net/events";
    public string HelpUrl { get; set; } = "https://rtub.azurewebsites.net/help";
}
