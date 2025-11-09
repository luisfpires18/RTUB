namespace RTUB.Web.EmailTemplates.Models;

/// <summary>
/// Model for password reset email template
/// </summary>
public class PasswordResetModel
{
    public string CallbackUrl { get; set; } = string.Empty;
}
