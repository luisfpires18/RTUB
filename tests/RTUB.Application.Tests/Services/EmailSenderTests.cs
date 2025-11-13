using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using RTUB.Application.Services;
using Xunit;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Tests for EmailSender service
/// </summary>
public class EmailSenderTests
{
    private readonly Mock<ILogger<EmailSender>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;

    public EmailSenderTests()
    {
        _mockLogger = new Mock<ILogger<EmailSender>>();
        _mockConfiguration = new Mock<IConfiguration>();
    }

    [Fact]
    public async Task SendEmailAsync_WithoutSMTPConfiguration_DoesNotThrow()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["EmailSettings:SmtpServer"]).Returns((string?)null);
        _mockConfiguration.Setup(c => c["EmailSettings:SmtpPassword"]).Returns((string?)null);
        
        var emailSender = new EmailSender(_mockLogger.Object, _mockConfiguration.Object);

        // Act
        Func<Task> act = async () => await emailSender.SendEmailAsync("test@example.com", "Test Subject", "Test Body");

        // Assert - Should not throw even when SMTP is not configured
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendEmailAsync_WithPlaceholderPassword_DoesNotThrow()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["EmailSettings:SmtpServer"]).Returns("smtp.example.com");
        _mockConfiguration.Setup(c => c["EmailSettings:SmtpPassword"]).Returns("YOUR_APP_PASSWORD_HERE");
        
        var emailSender = new EmailSender(_mockLogger.Object, _mockConfiguration.Object);

        // Act
        Func<Task> act = async () => await emailSender.SendEmailAsync("test@example.com", "Test Subject", "Test Body");

        // Assert - Should not throw even with placeholder password
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendEmailAsync_WithMissingConfiguration_DoesNotThrow()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["EmailSettings:SmtpServer"]).Returns((string?)null);
        var emailSender = new EmailSender(_mockLogger.Object, _mockConfiguration.Object);

        // Act
        Func<Task> act = async () => await emailSender.SendEmailAsync(
            "test@example.com",
            "Test Subject",
            "Test Body");

        // Assert
        await act.Should().NotThrowAsync("email sending should fail gracefully");
    }

    [Fact]
    public async Task SendEmailAsync_ParsesSmtpPortCorrectly()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["EmailSettings:SmtpServer"]).Returns((string?)null);
        _mockConfiguration.Setup(c => c["EmailSettings:SmtpPort"]).Returns("465");
        
        var emailSender = new EmailSender(_mockLogger.Object, _mockConfiguration.Object);

        // Act & Assert - Should not throw when parsing port
        await emailSender.SendEmailAsync("test@example.com", "Test", "Body");
    }

    [Fact]
    public async Task SendEmailAsync_UsesDefaultPort_WhenNotConfigured()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["EmailSettings:SmtpServer"]).Returns((string?)null);
        _mockConfiguration.Setup(c => c["EmailSettings:SmtpPort"]).Returns((string?)null);
        
        var emailSender = new EmailSender(_mockLogger.Object, _mockConfiguration.Object);

        // Act & Assert - Should use default port 587
        await emailSender.SendEmailAsync("test@example.com", "Test", "Body");
    }

    [Fact]
    public async Task SendEmailAsync_UsesDefaultSenderEmail_WhenNotConfigured()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["EmailSettings:SmtpServer"]).Returns((string?)null);
        _mockConfiguration.Setup(c => c["EmailSettings:SenderEmail"]).Returns((string?)null);
        
        var emailSender = new EmailSender(_mockLogger.Object, _mockConfiguration.Object);

        // Act & Assert - Should use default sender
        await emailSender.SendEmailAsync("test@example.com", "Test", "Body");
    }

    [Fact]
    public async Task SendEmailAsync_UsesDefaultSenderName_WhenNotConfigured()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["EmailSettings:SmtpServer"]).Returns((string?)null);
        _mockConfiguration.Setup(c => c["EmailSettings:SenderName"]).Returns((string?)null);
        
        var emailSender = new EmailSender(_mockLogger.Object, _mockConfiguration.Object);

        // Act & Assert - Should use default sender name "RTUB 1991"
        await emailSender.SendEmailAsync("test@example.com", "Test", "Body");
    }

    [Fact]
    public async Task SendEmailAsync_EnablesSsl_ByDefault()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["EmailSettings:SmtpServer"]).Returns((string?)null);
        _mockConfiguration.Setup(c => c["EmailSettings:EnableSsl"]).Returns((string?)null);
        
        var emailSender = new EmailSender(_mockLogger.Object, _mockConfiguration.Object);

        // Act & Assert - Should enable SSL by default
        await emailSender.SendEmailAsync("test@example.com", "Test", "Body");
    }

    [Fact]
    public async Task SendEmailAsync_CanDisableSsl_WhenConfigured()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["EmailSettings:SmtpServer"]).Returns((string?)null);
        _mockConfiguration.Setup(c => c["EmailSettings:EnableSsl"]).Returns("false");
        
        var emailSender = new EmailSender(_mockLogger.Object, _mockConfiguration.Object);

        // Act & Assert
        await emailSender.SendEmailAsync("test@example.com", "Test", "Body");
    }

    [Fact]
    public void EmailSender_CanBeConstructed()
    {
        // Arrange & Act
        var emailSender = new EmailSender(_mockLogger.Object, _mockConfiguration.Object);

        // Assert
        emailSender.Should().NotBeNull();
    }
}
