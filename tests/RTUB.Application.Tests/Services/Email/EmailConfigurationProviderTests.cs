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
public partial class EmailConfigurationProviderTests
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
