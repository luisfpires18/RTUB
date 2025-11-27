using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using RTUB.Application.Services.Email;

namespace RTUB.Application.Tests.Services.Email;

/// <summary>
/// Unit tests for EmailConfigurationProvider
/// Tests configuration loading and validation without any network calls
/// </summary>
public class EmailConfigurationProviderTests
{
    private readonly Mock<ILogger<EmailConfigurationProvider>> _mockLogger;

    public EmailConfigurationProviderTests()
    {
        _mockLogger = new Mock<ILogger<EmailConfigurationProvider>>();
    }

    [Fact]
    public void Constructor_WithNullConfiguration_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new EmailConfigurationProvider(null!, _mockLogger.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("configuration");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var mockConfig = new Mock<IConfiguration>();

        // Act
        var act = () => new EmailConfigurationProvider(mockConfig.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void GetConfiguration_ReturnsConfigurationWithAllValues()
    {
        // Arrange
        var configValues = new Dictionary<string, string?>
        {
            ["EmailSettings:RecipientEmail"] = "recipient@test.com",
            ["EmailSettings:SmtpServer"] = "smtp.test.com",
            ["EmailSettings:SmtpPort"] = "465",
            ["EmailSettings:SmtpUsername"] = "user@test.com",
            ["EmailSettings:SmtpPassword"] = "secretpassword",
            ["EmailSettings:SenderEmail"] = "sender@test.com",
            ["EmailSettings:SenderName"] = "Test Sender",
            ["EmailSettings:EnableSsl"] = "true"
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
        var provider = new EmailConfigurationProvider(config, _mockLogger.Object);

        // Act
        var result = provider.GetConfiguration();

        // Assert
        result.RecipientEmail.Should().Be("recipient@test.com");
        result.SmtpServer.Should().Be("smtp.test.com");
        result.SmtpPort.Should().Be(465);
        result.SmtpUsername.Should().Be("user@test.com");
        result.SmtpPassword.Should().Be("secretpassword");
        result.SenderEmail.Should().Be("sender@test.com");
        result.SenderName.Should().Be("Test Sender");
        result.EnableSsl.Should().BeTrue();
    }

    [Fact]
    public void GetConfiguration_UsesDefaultSmtpPort_WhenNotConfigured()
    {
        // Arrange
        var configValues = new Dictionary<string, string?>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
        var provider = new EmailConfigurationProvider(config, _mockLogger.Object);

        // Act
        var result = provider.GetConfiguration();

        // Assert
        result.SmtpPort.Should().Be(587);
    }

    [Fact]
    public void GetConfiguration_UsesDefaultSenderName_WhenNotConfigured()
    {
        // Arrange
        var configValues = new Dictionary<string, string?>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
        var provider = new EmailConfigurationProvider(config, _mockLogger.Object);

        // Act
        var result = provider.GetConfiguration();

        // Assert
        result.SenderName.Should().Be("RTUB 1991");
    }

    [Fact]
    public void GetConfiguration_EnablesSsl_ByDefault()
    {
        // Arrange
        var configValues = new Dictionary<string, string?>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
        var provider = new EmailConfigurationProvider(config, _mockLogger.Object);

        // Act
        var result = provider.GetConfiguration();

        // Assert
        result.EnableSsl.Should().BeTrue();
    }

    [Fact]
    public void GetConfiguration_DisablesSsl_WhenExplicitlyFalse()
    {
        // Arrange
        var configValues = new Dictionary<string, string?>
        {
            ["EmailSettings:EnableSsl"] = "false"
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
        var provider = new EmailConfigurationProvider(config, _mockLogger.Object);

        // Act
        var result = provider.GetConfiguration();

        // Assert
        result.EnableSsl.Should().BeFalse();
    }

    [Fact]
    public void IsSmtpConfigured_WithNullConfig_ReturnsFalse()
    {
        // Arrange
        var configValues = new Dictionary<string, string?>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
        var provider = new EmailConfigurationProvider(config, _mockLogger.Object);

        // Act
        var result = provider.IsSmtpConfigured(null!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsSmtpConfigured_WithMissingSmtpServer_ReturnsFalse()
    {
        // Arrange
        var configValues = new Dictionary<string, string?>
        {
            ["EmailSettings:SmtpUsername"] = "user",
            ["EmailSettings:SmtpPassword"] = "password"
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
        var provider = new EmailConfigurationProvider(config, _mockLogger.Object);
        var emailConfig = provider.GetConfiguration();

        // Act
        var result = provider.IsSmtpConfigured(emailConfig);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsSmtpConfigured_WithMissingSmtpUsername_ReturnsFalse()
    {
        // Arrange
        var configValues = new Dictionary<string, string?>
        {
            ["EmailSettings:SmtpServer"] = "smtp.test.com",
            ["EmailSettings:SmtpPassword"] = "password"
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
        var provider = new EmailConfigurationProvider(config, _mockLogger.Object);
        var emailConfig = provider.GetConfiguration();

        // Act
        var result = provider.IsSmtpConfigured(emailConfig);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsSmtpConfigured_WithMissingSmtpPassword_ReturnsFalse()
    {
        // Arrange
        var configValues = new Dictionary<string, string?>
        {
            ["EmailSettings:SmtpServer"] = "smtp.test.com",
            ["EmailSettings:SmtpUsername"] = "user"
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
        var provider = new EmailConfigurationProvider(config, _mockLogger.Object);
        var emailConfig = provider.GetConfiguration();

        // Act
        var result = provider.IsSmtpConfigured(emailConfig);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsSmtpConfigured_WithPlaceholderPassword_ReturnsFalse()
    {
        // Arrange
        var configValues = new Dictionary<string, string?>
        {
            ["EmailSettings:SmtpServer"] = "smtp.test.com",
            ["EmailSettings:SmtpUsername"] = "user",
            ["EmailSettings:SmtpPassword"] = "YOUR_APP_PASSWORD_HERE"
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
        var provider = new EmailConfigurationProvider(config, _mockLogger.Object);
        var emailConfig = provider.GetConfiguration();

        // Act
        var result = provider.IsSmtpConfigured(emailConfig);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsSmtpConfigured_WithValidConfiguration_ReturnsTrue()
    {
        // Arrange
        var configValues = new Dictionary<string, string?>
        {
            ["EmailSettings:SmtpServer"] = "smtp.test.com",
            ["EmailSettings:SmtpUsername"] = "user@test.com",
            ["EmailSettings:SmtpPassword"] = "realpassword"
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
        var provider = new EmailConfigurationProvider(config, _mockLogger.Object);
        var emailConfig = provider.GetConfiguration();

        // Act
        var result = provider.IsSmtpConfigured(emailConfig);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ValidateSenderEmail_WithNullConfig_ReturnsFalse()
    {
        // Arrange
        var configValues = new Dictionary<string, string?>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
        var provider = new EmailConfigurationProvider(config, _mockLogger.Object);

        // Act
        var result = provider.ValidateSenderEmail(null!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateSenderEmail_WithEmptySenderEmail_ReturnsFalse()
    {
        // Arrange
        var configValues = new Dictionary<string, string?>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
        var provider = new EmailConfigurationProvider(config, _mockLogger.Object);
        var emailConfig = provider.GetConfiguration();

        // Act
        var result = provider.ValidateSenderEmail(emailConfig);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateSenderEmail_WithValidSenderEmail_ReturnsTrue()
    {
        // Arrange
        var configValues = new Dictionary<string, string?>
        {
            ["EmailSettings:SenderEmail"] = "sender@test.com"
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
        var provider = new EmailConfigurationProvider(config, _mockLogger.Object);
        var emailConfig = provider.GetConfiguration();

        // Act
        var result = provider.ValidateSenderEmail(emailConfig);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ValidateRecipientEmail_WithNullConfig_ReturnsFalse()
    {
        // Arrange
        var configValues = new Dictionary<string, string?>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
        var provider = new EmailConfigurationProvider(config, _mockLogger.Object);

        // Act
        var result = provider.ValidateRecipientEmail(null!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateRecipientEmail_WithEmptyRecipientEmail_ReturnsFalse()
    {
        // Arrange
        var configValues = new Dictionary<string, string?>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
        var provider = new EmailConfigurationProvider(config, _mockLogger.Object);
        var emailConfig = provider.GetConfiguration();

        // Act
        var result = provider.ValidateRecipientEmail(emailConfig);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateRecipientEmail_WithValidRecipientEmail_ReturnsTrue()
    {
        // Arrange
        var configValues = new Dictionary<string, string?>
        {
            ["EmailSettings:RecipientEmail"] = "recipient@test.com"
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
        var provider = new EmailConfigurationProvider(config, _mockLogger.Object);
        var emailConfig = provider.GetConfiguration();

        // Act
        var result = provider.ValidateRecipientEmail(emailConfig);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("abc", 587)]
    [InlineData("", 587)]
    [InlineData("not-a-number", 587)]
    public void GetConfiguration_InvalidSmtpPort_UsesDefault(string invalidPort, int expectedPort)
    {
        // Arrange
        var configValues = new Dictionary<string, string?>
        {
            ["EmailSettings:SmtpPort"] = invalidPort
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
        var provider = new EmailConfigurationProvider(config, _mockLogger.Object);

        // Act
        var result = provider.GetConfiguration();

        // Assert
        result.SmtpPort.Should().Be(expectedPort);
    }
}
