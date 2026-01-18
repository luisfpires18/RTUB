using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;

namespace RTUB.Application.Tests.Configuration;

/// <summary>
/// Unit tests for BmrBebeMaisRui game configuration
/// </summary>
public class BmrBebeMaisRuiConfigurationTests
{
    [Fact]
    public void BmrBebeMaisRuiConfiguration_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var config = new BmrBebeMaisRuiConfiguration();

        // Assert
        config.StartingHealth.Should().Be(100, "StartingHealth should be 100 by default");
        config.InvulnerabilityMs.Should().Be(1500, "InvulnerabilityMs should be 1500 by default");
        config.JumpStrength.Should().Be(650, "JumpStrength should be 650 by default");
        config.Gravity.Should().Be(1200, "Gravity should be 1200 by default");
        config.MoveSpeed.Should().Be(200, "MoveSpeed should be 200 by default");
        config.BeerPoints.Should().Be(10, "BeerPoints should be 10 by default");
        config.LevelUpSeconds.Should().Be(30, "LevelUpSeconds should be 30 by default");
    }

    [Fact]
    public void BmrBebeMaisRuiConfiguration_DifficultyScaling_DefaultValues()
    {
        // Arrange & Act
        var config = new BmrBebeMaisRuiConfiguration();

        // Assert
        config.DifficultyScaling.Should().NotBeNull();
        config.DifficultyScaling.BaseSpawnRate.Should().Be(3.5);
        config.DifficultyScaling.SpawnRateDecreasePerLevel.Should().Be(0.15);
        config.DifficultyScaling.MinSpawnRate.Should().Be(1.0);
        config.DifficultyScaling.EnemySpeedIncreasePerLevel.Should().Be(5);
        config.DifficultyScaling.PlatformGapIncreasePerLevel.Should().Be(5);
    }

    [Fact]
    public void BmrBebeMaisRuiConfiguration_EnemyTiers_HasFourTiersByDefault()
    {
        // Arrange & Act
        var config = new BmrBebeMaisRuiConfiguration();

        // Assert
        config.EnemyTiers.Should().HaveCount(4);
        config.EnemyTiers.Should().Contain(t => t.Name == "Chubby");
        config.EnemyTiers.Should().Contain(t => t.Name == "Plus");
        config.EnemyTiers.Should().Contain(t => t.Name == "Heavy");
        config.EnemyTiers.Should().Contain(t => t.Name == "Mega");
    }

    [Fact]
    public void BmrBebeMaisRuiConfiguration_EnemyTiers_DamageIncreasesWithSize()
    {
        // Arrange & Act
        var config = new BmrBebeMaisRuiConfiguration();

        // Assert - heavier tiers should do more damage
        var chubby = config.EnemyTiers.First(t => t.Name == "Chubby");
        var plus = config.EnemyTiers.First(t => t.Name == "Plus");
        var heavy = config.EnemyTiers.First(t => t.Name == "Heavy");
        var mega = config.EnemyTiers.First(t => t.Name == "Mega");

        chubby.Damage.Should().BeLessThan(plus.Damage);
        plus.Damage.Should().BeLessThan(heavy.Damage);
        heavy.Damage.Should().BeLessThan(mega.Damage);
    }

    [Fact]
    public void BmrBebeMaisRuiConfiguration_EnemyTiers_SpeedDecreasesWithSize()
    {
        // Arrange & Act
        var config = new BmrBebeMaisRuiConfiguration();

        // Assert - heavier tiers should be slower
        var chubby = config.EnemyTiers.First(t => t.Name == "Chubby");
        var plus = config.EnemyTiers.First(t => t.Name == "Plus");
        var heavy = config.EnemyTiers.First(t => t.Name == "Heavy");
        var mega = config.EnemyTiers.First(t => t.Name == "Mega");

        chubby.Speed.Should().BeGreaterThan(plus.Speed);
        plus.Speed.Should().BeGreaterThan(heavy.Speed);
        heavy.Speed.Should().BeGreaterThan(mega.Speed);
    }

    [Fact]
    public void BmrBebeMaisRuiConfiguration_CanBeConfigured_FromConfiguration()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            { "Games:BmrBebeMaisRui:StartingHealth", "150" },
            { "Games:BmrBebeMaisRui:InvulnerabilityMs", "2000" },
            { "Games:BmrBebeMaisRui:JumpStrength", "500" },
            { "Games:BmrBebeMaisRui:Gravity", "1000" },
            { "Games:BmrBebeMaisRui:MoveSpeed", "250" },
            { "Games:BmrBebeMaisRui:BeerPoints", "15" },
            { "Games:BmrBebeMaisRui:LevelUpSeconds", "45" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.Configure<BmrBebeMaisRuiConfiguration>(configuration.GetSection(BmrBebeMaisRuiConfiguration.SectionName));
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var config = serviceProvider.GetRequiredService<IOptions<BmrBebeMaisRuiConfiguration>>().Value;

        // Assert
        config.StartingHealth.Should().Be(150);
        config.InvulnerabilityMs.Should().Be(2000);
        config.JumpStrength.Should().Be(500);
        config.Gravity.Should().Be(1000);
        config.MoveSpeed.Should().Be(250);
        config.BeerPoints.Should().Be(15);
        config.LevelUpSeconds.Should().Be(45);
    }

    [Fact]
    public void BmrBebeMaisRuiConfiguration_SectionName_IsCorrect()
    {
        // Assert
        BmrBebeMaisRuiConfiguration.SectionName.Should().Be("Games:BmrBebeMaisRui");
    }

    [Fact]
    public void BmrBebeMaisRuiConfiguration_MissingConfiguration_UsesDefaultValues()
    {
        // Arrange - empty configuration
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();
        services.Configure<BmrBebeMaisRuiConfiguration>(configuration.GetSection(BmrBebeMaisRuiConfiguration.SectionName));
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var config = serviceProvider.GetRequiredService<IOptions<BmrBebeMaisRuiConfiguration>>().Value;

        // Assert - should use default values
        config.StartingHealth.Should().Be(100);
        config.InvulnerabilityMs.Should().Be(1500);
        config.JumpStrength.Should().Be(650);
        config.Gravity.Should().Be(1200);
        config.MoveSpeed.Should().Be(200);
        config.BeerPoints.Should().Be(10);
        config.LevelUpSeconds.Should().Be(30);
    }

    [Fact]
    public void BmrBebeMaisRuiConfiguration_DifficultyScaling_CanBeConfigured()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            { "Games:BmrBebeMaisRui:DifficultyScaling:BaseSpawnRate", "3.0" },
            { "Games:BmrBebeMaisRui:DifficultyScaling:SpawnRateDecreasePerLevel", "0.2" },
            { "Games:BmrBebeMaisRui:DifficultyScaling:MinSpawnRate", "0.3" },
            { "Games:BmrBebeMaisRui:DifficultyScaling:EnemySpeedIncreasePerLevel", "10" },
            { "Games:BmrBebeMaisRui:DifficultyScaling:PlatformGapIncreasePerLevel", "8" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.Configure<BmrBebeMaisRuiConfiguration>(configuration.GetSection(BmrBebeMaisRuiConfiguration.SectionName));
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var config = serviceProvider.GetRequiredService<IOptions<BmrBebeMaisRuiConfiguration>>().Value;

        // Assert
        config.DifficultyScaling.BaseSpawnRate.Should().Be(3.0);
        config.DifficultyScaling.SpawnRateDecreasePerLevel.Should().Be(0.2);
        config.DifficultyScaling.MinSpawnRate.Should().Be(0.3);
        config.DifficultyScaling.EnemySpeedIncreasePerLevel.Should().Be(10);
        config.DifficultyScaling.PlatformGapIncreasePerLevel.Should().Be(8);
    }
}
