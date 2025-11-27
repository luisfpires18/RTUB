using FluentAssertions;
using RTUB.Application.Services.Email;

namespace RTUB.Application.Tests.Services.Email;

/// <summary>
/// Unit tests for SmtpClientFactory
/// Tests SMTP client creation and configuration without actual network calls
/// </summary>
public class SmtpClientFactoryTests
{
    private readonly SmtpClientFactory _factory;

    public SmtpClientFactoryTests()
    {
        _factory = new SmtpClientFactory();
    }

    [Fact]
    public void CreateClient_WithNullConfig_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _factory.CreateClient(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("config");
    }

    [Fact]
    public void CreateClient_WithMissingSmtpUsername_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = new EmailConfiguration
        {
            SmtpServer = "smtp.test.com",
            SmtpPassword = "password"
        };

        // Act
        var act = () => _factory.CreateClient(config);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*SMTP credentials are not configured*");
    }

    [Fact]
    public void CreateClient_WithMissingSmtpPassword_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = new EmailConfiguration
        {
            SmtpServer = "smtp.test.com",
            SmtpUsername = "user"
        };

        // Act
        var act = () => _factory.CreateClient(config);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*SMTP credentials are not configured*");
    }

    [Fact]
    public void CreateClient_WithMissingSmtpServer_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = new EmailConfiguration
        {
            SmtpUsername = "user",
            SmtpPassword = "password"
        };

        // Act
        var act = () => _factory.CreateClient(config);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*SMTP server is not configured*");
    }

    [Fact]
    public void CreateClient_WithValidConfig_ReturnsSmtpClient()
    {
        // Arrange
        var config = new EmailConfiguration
        {
            SmtpServer = "smtp.test.com",
            SmtpPort = 587,
            SmtpUsername = "user@test.com",
            SmtpPassword = "password",
            EnableSsl = true
        };

        // Act
        using var client = _factory.CreateClient(config);

        // Assert
        client.Should().NotBeNull();
        client.Host.Should().Be("smtp.test.com");
        client.Port.Should().Be(587);
        client.EnableSsl.Should().BeTrue();
    }

    [Fact]
    public void CreateClient_WithCustomPort_UsesCustomPort()
    {
        // Arrange
        var config = new EmailConfiguration
        {
            SmtpServer = "smtp.test.com",
            SmtpPort = 465,
            SmtpUsername = "user@test.com",
            SmtpPassword = "password",
            EnableSsl = true
        };

        // Act
        using var client = _factory.CreateClient(config);

        // Assert
        client.Port.Should().Be(465);
    }

    [Fact]
    public void CreateClient_WithSslDisabled_DisablesSsl()
    {
        // Arrange
        var config = new EmailConfiguration
        {
            SmtpServer = "smtp.test.com",
            SmtpPort = 25,
            SmtpUsername = "user@test.com",
            SmtpPassword = "password",
            EnableSsl = false
        };

        // Act
        using var client = _factory.CreateClient(config);

        // Assert
        client.EnableSsl.Should().BeFalse();
    }

    [Fact]
    public void CreateClient_WithCustomTimeout_SetsTimeout()
    {
        // Arrange
        var config = new EmailConfiguration
        {
            SmtpServer = "smtp.test.com",
            SmtpPort = 587,
            SmtpUsername = "user@test.com",
            SmtpPassword = "password",
            EnableSsl = true
        };
        var customTimeout = 60000;

        // Act
        using var client = _factory.CreateClient(config, customTimeout);

        // Assert
        client.Timeout.Should().Be(customTimeout);
    }

    [Fact]
    public void CreateClient_WithDefaultTimeout_UsesDefaultTimeout()
    {
        // Arrange
        var config = new EmailConfiguration
        {
            SmtpServer = "smtp.test.com",
            SmtpPort = 587,
            SmtpUsername = "user@test.com",
            SmtpPassword = "password",
            EnableSsl = true
        };

        // Act
        using var client = _factory.CreateClient(config);

        // Assert
        client.Timeout.Should().Be(10000); // Default timeout
    }

    [Fact]
    public void BatchEmailTimeout_HasCorrectValue()
    {
        // Assert
        SmtpClientFactory.BatchEmailTimeout.Should().Be(30000);
    }

    [Theory]
    [InlineData("smtp.gmail.com", 587)]
    [InlineData("smtp.outlook.com", 587)]
    [InlineData("mail.example.com", 465)]
    public void CreateClient_WithDifferentServers_ConfiguresCorrectly(string server, int port)
    {
        // Arrange
        var config = new EmailConfiguration
        {
            SmtpServer = server,
            SmtpPort = port,
            SmtpUsername = "user@test.com",
            SmtpPassword = "password",
            EnableSsl = true
        };

        // Act
        using var client = _factory.CreateClient(config);

        // Assert
        client.Host.Should().Be(server);
        client.Port.Should().Be(port);
    }
}
