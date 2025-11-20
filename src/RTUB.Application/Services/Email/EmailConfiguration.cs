namespace RTUB.Application.Services.Email;

/// <summary>
/// Email configuration model containing SMTP and sender settings
/// </summary>
public class EmailConfiguration
{
    private const string DefaultSenderName = "RTUB 1991";

    public string? RecipientEmail { get; set; }
    public string? SmtpServer { get; set; }
    public int SmtpPort { get; set; }
    public string? SmtpUsername { get; set; }
    public string? SmtpPassword { get; set; }
    public string? SenderEmail { get; set; }
    public string SenderName { get; set; } = DefaultSenderName;
    public bool EnableSsl { get; set; }
}
