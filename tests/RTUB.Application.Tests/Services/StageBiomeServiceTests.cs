using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RTUB.Application.Configuration;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using Xunit;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for StageBiomeService
/// Tests biome resolution, enemy counting, boss detection, and stat scaling
/// </summary>
public class StageBiomeServiceTests
{
    private readonly Mock<IWebHostEnvironment> _environmentMock;
    private readonly Mock<ILogger<StageBiomeService>> _loggerMock;
    private readonly Mock<IStageEnemyRepository> _stageEnemyRepositoryMock;
    private readonly MyTunoScalingConfiguration _config;
    private readonly StageBiomeService _service;

    public StageBiomeServiceTests()
    {
        _environmentMock = new Mock<IWebHostEnvironment>();
        _loggerMock = new Mock<ILogger<StageBiomeService>>();
        _stageEnemyRepositoryMock = new Mock<IStageEnemyRepository>();

        // Setup configuration
        _config = new MyTunoScalingConfiguration
        {
            StageMode = new StageModeConfig
            {
                Biomes = new List<BiomeConfig>
                {
                    new BiomeConfig
                    {
                        Name = "Forest",
                        StageMin = 1,
                        StageMax = 100,
                        EnemySpritePath = "sprites/games/my-tuno/enemies/forest",
                        BossSpritePrefix = "boss_"
                    },
                    new BiomeConfig
                    {
                        Name = "Desert",
                        StageMin = 101,
                        StageMax = 200,
                        EnemySpritePath = "sprites/games/my-tuno/enemies/desert",
                        BossSpritePrefix = "boss_"
                    }
                },
                EncounterRules = new EncounterRulesConfig
                {
                    BossEveryNStages = 10,
                    EnemyCountByStageOffset = new List<EnemyCountRule>
                    {
                        new EnemyCountRule { From = 1, To = 2, Count = 1 },
                        new EnemyCountRule { From = 3, To = 4, Count = 2 },
                        new EnemyCountRule { From = 5, To = 6, Count = 3 },
                        new EnemyCountRule { From = 7, To = 8, Count = 4 },
                        new EnemyCountRule { From = 9, To = 9, Count = 5 }
                    }
                },
                Scaling = new StageScalingConfig
                {
                    HpGrowthPerStage = 0.06,
                    DamageGrowthPerStage = 0.05,
                    ArmorGrowthPerStage = 0.03,
                    BossMultiplier = 2.5
                }
            }
        };

        var optionsMock = new Mock<IOptions<MyTunoScalingConfiguration>>();
        optionsMock.Setup(o => o.Value).Returns(_config);

        _service = new StageBiomeService(optionsMock.Object, _environmentMock.Object, _loggerMock.Object, _stageEnemyRepositoryMock.Object);
    }

    #region Biome Resolution Tests

    [Fact]
    public void GetBiomeForStage_Stage1_ReturnsForest()
    {
        // Act
        var biome = _service.GetBiomeForStage(1);

        // Assert
        Assert.Equal("Forest", biome);
    }

    [Fact]
    public void GetBiomeForStage_Stage50_ReturnsForest()
    {
        // Act
        var biome = _service.GetBiomeForStage(50);

        // Assert
        Assert.Equal("Forest", biome);
    }

    [Fact]
    public void GetBiomeForStage_Stage100_ReturnsForest()
    {
        // Act
        var biome = _service.GetBiomeForStage(100);

        // Assert
        Assert.Equal("Forest", biome);
    }

    [Fact]
    public void GetBiomeForStage_Stage101_ReturnsDesert()
    {
        // Act
        var biome = _service.GetBiomeForStage(101);

        // Assert
        Assert.Equal("Desert", biome);
    }

    [Fact]
    public void GetBiomeForStage_Stage200_ReturnsDesert()
    {
        // Act
        var biome = _service.GetBiomeForStage(200);

        // Assert
        Assert.Equal("Desert", biome);
    }

    [Fact]
    public void GetBiomeForStage_Stage999_ReturnsLastBiome()
    {
        // Act
        var biome = _service.GetBiomeForStage(999);

        // Assert
        Assert.Equal("Desert", biome); // Should return last configured biome
    }

    #endregion

    #region Enemy Count Tests

    [Fact]
    public void GetEnemyCountForStage_Stage1_Returns1()
    {
        // Act
        var count = _service.GetEnemyCountForStage(1);

        // Assert
        Assert.Equal(1, count);
    }

    [Fact]
    public void GetEnemyCountForStage_Stage2_Returns1()
    {
        // Act
        var count = _service.GetEnemyCountForStage(2);

        // Assert
        Assert.Equal(1, count);
    }

    [Fact]
    public void GetEnemyCountForStage_Stage3_Returns2()
    {
        // Act
        var count = _service.GetEnemyCountForStage(3);

        // Assert
        Assert.Equal(2, count);
    }

    [Fact]
    public void GetEnemyCountForStage_Stage4_Returns2()
    {
        // Act
        var count = _service.GetEnemyCountForStage(4);

        // Assert
        Assert.Equal(2, count);
    }

    [Fact]
    public void GetEnemyCountForStage_Stage5_Returns3()
    {
        // Act
        var count = _service.GetEnemyCountForStage(5);

        // Assert
        Assert.Equal(3, count);
    }

    [Fact]
    public void GetEnemyCountForStage_Stage9_Returns5()
    {
        // Act
        var count = _service.GetEnemyCountForStage(9);

        // Assert
        Assert.Equal(5, count);
    }

    [Fact]
    public void GetEnemyCountForStage_Stage11_Returns1()
    {
        // Offset in second "decade": 11 % 10 = 1, offset = 1
        // Act
        var count = _service.GetEnemyCountForStage(11);

        // Assert
        Assert.Equal(1, count);
    }

    [Fact]
    public void GetEnemyCountForStage_Stage19_Returns5()
    {
        // Offset in second "decade": 19 % 10 = 9, offset = 9
        // Act
        var count = _service.GetEnemyCountForStage(19);

        // Assert
        Assert.Equal(5, count);
    }

    #endregion

    #region Boss Stage Tests

    [Fact]
    public void IsBossStage_Stage10_ReturnsTrue()
    {
        // Act
        var isBoss = _service.IsBossStage(10);

        // Assert
        Assert.True(isBoss);
    }

    [Fact]
    public void IsBossStage_Stage20_ReturnsTrue()
    {
        // Act
        var isBoss = _service.IsBossStage(20);

        // Assert
        Assert.True(isBoss);
    }

    [Fact]
    public void IsBossStage_Stage100_ReturnsTrue()
    {
        // Act
        var isBoss = _service.IsBossStage(100);

        // Assert
        Assert.True(isBoss);
    }

    [Fact]
    public void IsBossStage_Stage9_ReturnsFalse()
    {
        // Act
        var isBoss = _service.IsBossStage(9);

        // Assert
        Assert.False(isBoss);
    }

    [Fact]
    public void IsBossStage_Stage11_ReturnsFalse()
    {
        // Act
        var isBoss = _service.IsBossStage(11);

        // Assert
        Assert.False(isBoss);
    }

    [Fact]
    public void GetEnemyCountForStage_BossStage10_Returns1()
    {
        // Boss stages should always have 1 enemy (the boss)
        // Act
        var count = _service.GetEnemyCountForStage(10);

        // Assert
        Assert.Equal(1, count);
    }

    #endregion

    #region Stat Scaling Tests

    [Fact]
    public void CalculateScaledStats_Stage1_ReturnsBaseStats()
    {
        // Act
        var (hp, damage) = _service.CalculateScaledStats(1, 100, 10, isBoss: false);

        // Assert
        Assert.Equal(100, hp);
        Assert.Equal(10, damage);
    }

    [Fact]
    public void CalculateScaledStats_Stage2_ReturnsScaledStats()
    {
        // With 6% HP growth and 5% damage growth
        // Stage 2: HP = 100 * (1.06)^1 = 106, Damage = 10 * (1.05)^1 = 10.5 = 10
        // Act
        var (hp, damage) = _service.CalculateScaledStats(2, 100, 10, isBoss: false);

        // Assert
        Assert.True(hp > 100, "HP should scale up");
        Assert.True(damage >= 10, "Damage should scale up or stay same");
    }

    [Fact]
    public void CalculateScaledStats_Stage10_Boss_AppliesBossMultiplier()
    {
        // Boss multiplier should be applied (2.5x)
        // Act
        var (hp, damage) = _service.CalculateScaledStats(10, 100, 10, isBoss: true);

        // Assert
        // Should be significantly higher due to both stage scaling and boss multiplier
        Assert.True(hp > 200, $"Boss HP should be > 200, got {hp}");
        Assert.True(damage > 20, $"Boss damage should be > 20, got {damage}");
    }

    [Fact]
    public void CalculateScaledStats_Stage10_NonBoss_NoMultiplier()
    {
        // Regular enemy should not get boss multiplier
        // Act
        var (hpNormal, damageNormal) = _service.CalculateScaledStats(10, 100, 10, isBoss: false);
        var (hpBoss, damageBoss) = _service.CalculateScaledStats(10, 100, 10, isBoss: true);

        // Assert
        Assert.True(hpBoss > hpNormal, "Boss should have more HP than normal enemy");
        Assert.True(damageBoss > damageNormal, "Boss should have more damage than normal enemy");
    }

    [Fact]
    public void CalculateScaledStats_Stage50_ShowsExponentialGrowth()
    {
        // Test that stats grow exponentially
        // Act
        var (hp10, _) = _service.CalculateScaledStats(10, 100, 10, isBoss: false);
        var (hp50, _) = _service.CalculateScaledStats(50, 100, 10, isBoss: false);

        // Assert
        // HP at stage 50 should be much higher than at stage 10 due to exponential growth
        Assert.True(hp50 > hp10 * 5, $"Stage 50 HP ({hp50}) should be > 5x stage 10 HP ({hp10})");
    }

    #endregion

    #region Configuration Edge Cases

    [Fact]
    public void GetBiomeForStage_EmptyBiomes_ReturnsDefault()
    {
        // Arrange
        var emptyConfig = new MyTunoScalingConfiguration
        {
            StageMode = new StageModeConfig
            {
                Biomes = new List<BiomeConfig>()
            }
        };
        var optionsMock = new Mock<IOptions<MyTunoScalingConfiguration>>();
        optionsMock.Setup(o => o.Value).Returns(emptyConfig);
        var service = new StageBiomeService(optionsMock.Object, _environmentMock.Object, _loggerMock.Object, _stageEnemyRepositoryMock.Object);

        // Act
        var biome = service.GetBiomeForStage(1);

        // Assert
        Assert.Equal("Forest", biome); // Should return default
    }

    [Fact]
    public void GetEnemyCountForStage_EmptyRules_ReturnsDefault()
    {
        // Arrange
        var emptyConfig = new MyTunoScalingConfiguration
        {
            StageMode = new StageModeConfig
            {
                EncounterRules = new EncounterRulesConfig
                {
                    BossEveryNStages = 10,
                    EnemyCountByStageOffset = new List<EnemyCountRule>()
                }
            }
        };
        var optionsMock = new Mock<IOptions<MyTunoScalingConfiguration>>();
        optionsMock.Setup(o => o.Value).Returns(emptyConfig);
        var service = new StageBiomeService(optionsMock.Object, _environmentMock.Object, _loggerMock.Object, _stageEnemyRepositoryMock.Object);

        // Act
        var count = service.GetEnemyCountForStage(5);

        // Assert
        Assert.Equal(1, count); // Should return default of 1
    }

    #endregion
}
