using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace RTUB.Web.Tests;

/// <summary>
/// Tests for configuration loading to ensure environment-specific settings are applied
/// </summary>
public class ConfigurationTests
{
    [Fact]
    public void Configuration_LoadsProductionSettings_WhenProductionEnvironment()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Production.json", optional: true)
            .Build();

        // Act
        var smtpUsername = configuration["EmailSettings:SmtpUsername"];

        // Assert
        // If appsettings.Production.json is loaded, it should override the dev settings
        smtpUsername.Should().NotBeNull("EmailSettings:SmtpUsername should be configured");
    }

    [Fact]
    public void Configuration_SupportsEnvironmentSpecificFiles()
    {
        // Arrange
        var environmentName = "Production";
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
            .Build();

        // Act
        var emailSettings = configuration.GetSection("EmailSettings");

        // Assert
        emailSettings.Exists().Should().BeTrue("EmailSettings section should exist");
        emailSettings["SmtpServer"].Should().NotBeNullOrEmpty("SmtpServer should be configured");
        emailSettings["SmtpPort"].Should().NotBeNullOrEmpty("SmtpPort should be configured");
    }
}
