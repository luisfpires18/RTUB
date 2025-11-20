using System.Net;
using System.Net.Mail;

namespace RTUB.Application.Services.Email;

/// <summary>
/// Factory for creating and configuring SMTP clients
/// Follows Single Responsibility Principle - only handles SMTP client creation
/// </summary>
public class SmtpClientFactory
{
    private const int DefaultSmtpTimeout = 10000;
    public const int BatchEmailTimeout = 30000;

    /// <summary>
    /// Creates and configures an SMTP client with default timeout
    /// </summary>
    public SmtpClient CreateClient(EmailConfiguration config)
    {
        return CreateClient(config, DefaultSmtpTimeout);
    }

    /// <summary>
    /// Creates and configures an SMTP client with specified timeout
    /// </summary>
    public SmtpClient CreateClient(EmailConfiguration config, int timeout)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        if (string.IsNullOrEmpty(config.SmtpUsername) || string.IsNullOrEmpty(config.SmtpPassword))
        {
            throw new InvalidOperationException(
                "SMTP credentials are not configured. Check EmailSettings:SmtpUsername and EmailSettings:SmtpPassword settings.");
        }

        if (string.IsNullOrEmpty(config.SmtpServer))
        {
            throw new InvalidOperationException(
                "SMTP server is not configured. Check EmailSettings:SmtpServer setting.");
        }

        return new SmtpClient(config.SmtpServer, config.SmtpPort)
        {
            Credentials = new NetworkCredential(config.SmtpUsername, config.SmtpPassword),
            EnableSsl = config.EnableSsl,
            Timeout = timeout
        };
    }
}
