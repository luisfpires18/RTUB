namespace RTUB.Application.Interfaces;

/// <summary>
/// Sends one HTML email over SMTP (<c>EmailSender</c>); used by the password-recovery flow. Same shape as the
/// Identity UI <c>IEmailSender</c>, without that package.
/// </summary>
public interface IEmailSender
{
    Task SendEmailAsync(string email, string subject, string htmlMessage);
}
