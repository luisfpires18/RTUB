using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace RTUB.Application.Services.Email;

/// <summary>
/// Provides email configuration from application settings
/// Follows Single Responsibility Principle - only handles configuration loading and validation
/// </summary>
public class EmailConfigurationProvider
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailConfigurationProvider> _logger;

    // Configuration key constants
    private const string EmailSettingsPrefix = "EmailSettings:";
    private const string RecipientEmailKey = EmailSettingsPrefix + "RecipientEmail";
    private const string SmtpServerKey = EmailSettingsPrefix + "SmtpServer";
    private const string SmtpPortKey = EmailSettingsPrefix + "SmtpPort";
    private const string SmtpUsernameKey = EmailSettingsPrefix + "SmtpUsername";
    private const string SmtpPasswordKey = EmailSettingsPrefix + "SmtpPassword";
    private const string SenderEmailKey = EmailSettingsPrefix + "SenderEmail";
    private const string SenderNameKey = EmailSettingsPrefix + "SenderName";
    private const string EnableSslKey = EmailSettingsPrefix + "EnableSsl";
    private const string DefaultSenderName = "RTUB 1991";
    private const string PlaceholderPassword = "YOUR_APP_PASSWORD_HERE";
    private const int DefaultSmtpPort = 587;

    public EmailConfigurationProvider(
        IConfiguration configuration,
        ILogger<EmailConfigurationProvider> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets email configuration from application settings
    /// </summary>
    public EmailConfiguration GetConfiguration()
    {
        var config = new EmailConfiguration
        {
            RecipientEmail = _configuration[RecipientEmailKey],
            SmtpServer = _configuration[SmtpServerKey],
            SmtpPort = int.TryParse(_configuration[SmtpPortKey], out var port) ? port : DefaultSmtpPort,
            SmtpUsername = _configuration[SmtpUsernameKey],
            SmtpPassword = _configuration[SmtpPasswordKey],
            SenderEmail = _configuration[SenderEmailKey],
            SenderName = _configuration[SenderNameKey] ?? DefaultSenderName,
            EnableSsl = _configuration[EnableSslKey] != "false" // Default to true
        };

        return config;
    }

    /// <summary>
    /// Validates that SMTP is properly configured
    /// </summary>
    public bool IsSmtpConfigured(EmailConfiguration config)
    {
        if (config == null)
        {
            _logger.LogWarning("Email configuration is null");
            return false;
        }

        if (string.IsNullOrEmpty(config.SmtpServer))
        {
            _logger.LogWarning("SMTP server is not configured");
            return false;
        }

        if (string.IsNullOrEmpty(config.SmtpUsername))
        {
            _logger.LogWarning("SMTP username is not configured");
            return false;
        }

        if (string.IsNullOrEmpty(config.SmtpPassword))
        {
            _logger.LogWarning("SMTP password is not configured");
            return false;
        }

        if (config.SmtpPassword == PlaceholderPassword)
        {
            _logger.LogWarning("SMTP password is still set to placeholder value");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates sender email configuration
    /// </summary>
    public bool ValidateSenderEmail(EmailConfiguration config)
    {
        if (string.IsNullOrEmpty(config?.SenderEmail))
        {
            _logger.LogError("SenderEmail is not configured in EmailSettings");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates recipient email configuration
    /// </summary>
    public bool ValidateRecipientEmail(EmailConfiguration config)
    {
        if (string.IsNullOrEmpty(config?.RecipientEmail))
        {
            _logger.LogError("RecipientEmail is not configured in EmailSettings");
            return false;
        }

        return true;
    }
}
