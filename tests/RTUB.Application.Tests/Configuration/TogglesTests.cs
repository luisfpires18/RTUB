using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;

namespace RTUB.Application.Tests.Configuration;

/// <summary>
/// Unit tests for Toggles configuration
/// </summary>
public class TogglesTests
{
    [Fact]
    public void Toggles_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var settings = new Toggles();

        // Assert
        settings.MapEnabled.Should().BeTrue("MapEnabled should be true by default");
    }

    [Fact]
    public void Toggles_CanBeConfigured_FromConfiguration()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            { "Toggles:MapEnabled", "false" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.Configure<Toggles>(configuration.GetSection(Toggles.SectionName));
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var settings = serviceProvider.GetRequiredService<IOptions<Toggles>>().Value;

        // Assert
        settings.MapEnabled.Should().BeFalse("MapEnabled was set to false in configuration");
    }

    [Fact]
    public void Toggles_MapEnabled_CanBeTrue()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            { "Toggles:MapEnabled", "true" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.Configure<Toggles>(configuration.GetSection(Toggles.SectionName));
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var settings = serviceProvider.GetRequiredService<IOptions<Toggles>>().Value;

        // Assert
        settings.MapEnabled.Should().BeTrue("MapEnabled was set to true in configuration");
    }

    [Fact]
    public void Toggles_SectionName_IsCorrect()
    {
        // Assert
        Toggles.SectionName.Should().Be("Toggles");
    }

    [Fact]
    public void Toggles_MissingConfiguration_UsesDefaultValue()
    {
        // Arrange - empty configuration
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();
        services.Configure<Toggles>(configuration.GetSection(Toggles.SectionName));
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var settings = serviceProvider.GetRequiredService<IOptions<Toggles>>().Value;

        // Assert
        settings.MapEnabled.Should().BeTrue("Should use default value when configuration is missing");
    }
}
