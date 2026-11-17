using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using RTUB.Application.Services.Email;

namespace RTUB.Application.Tests.Services.Email;

// EmailConfigurationProviderTests: Smtp And Address Validation tests.
public partial class EmailConfigurationProviderTests
{
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
}
