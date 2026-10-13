namespace RTUB.Application.Interfaces;

/// <summary>
/// Sends one HTML email over SMTP (<c>EmailSender</c>); used by the password-recovery flow. Same shape as the
/// Identity UI interface it replaces (task 042), which was the app's only use of that package.
/// </summary>
public interface IEmailSender
{
    Task SendEmailAsync(string email, string subject, string htmlMessage);
}
