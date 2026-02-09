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
                        Name = "Swamp",
                        StageMin = 101,
                        StageMax = 200,
                        EnemySpritePath = "sprites/games/my-tuno/enemies/swamp",
                        BossSpritePrefix = "boss_"
                    },
                    new BiomeConfig
                    {
                        Name = "Mountains",
                        StageMin = 201,
                        StageMax = 300,
                        EnemySpritePath = "sprites/games/my-tuno/enemies/mountains",
                        BossSpritePrefix = "boss_"
                    },
                    new BiomeConfig
                    {
                        Name = "Snowy",
                        StageMin = 301,
                        StageMax = 400,
                        EnemySpritePath = "sprites/games/my-tuno/enemies/snowy",
                        BossSpritePrefix = "boss_"
                    },
                    new BiomeConfig
                    {
                        Name = "Tropical",
                        StageMin = 401,
                        StageMax = 500,
                        EnemySpritePath = "sprites/games/my-tuno/enemies/tropical",
                        BossSpritePrefix = "boss_"
                    },
                    new BiomeConfig
                    {
                        Name = "Caverns",
                        StageMin = 501,
                        StageMax = 600,
                        EnemySpritePath = "sprites/games/my-tuno/enemies/caverns",
                        BossSpritePrefix = "boss_"
                    },
                    new BiomeConfig
                    {
                        Name = "Desert",
                        StageMin = 601,
                        StageMax = 700,
                        EnemySpritePath = "sprites/games/my-tuno/enemies/forest",
                        BossSpritePrefix = "boss_"
                    },
                    new BiomeConfig
                    {
                        Name = "Volcanic",
                        StageMin = 701,
                        StageMax = 800,
                        EnemySpritePath = "sprites/games/my-tuno/enemies/forest",
                        BossSpritePrefix = "boss_"
                    },
                    new BiomeConfig
                    {
                        Name = "Ruins",
                        StageMin = 801,
                        StageMax = 900,
                        EnemySpritePath = "sprites/games/my-tuno/enemies/ruins",
                        BossSpritePrefix = "boss_"
                    },
                    new BiomeConfig
                    {
                        Name = "Dark",
                        StageMin = 901,
                        StageMax = 1000,
                        EnemySpritePath = "sprites/games/my-tuno/enemies/forest",
                        BossSpritePrefix = "boss_"
                    },
                    new BiomeConfig
                    {
                        Name = "Void",
                        StageMin = 1001,
                        StageMax = 999999999,
                        EnemySpritePath = "sprites/games/my-tuno/enemies/void",
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

                DifficultyCurve = new DifficultyCurveConfig
                {
                    ScalingRate = 0.12,
                    GrowthExponent = 1.15
                },
                RewardCurve = new RewardCurveConfig
                {
                    ScalingRate = 0.08,
                    GrowthExponent = 1.10,
                    LevelBonusPerLevel = 0.04,
                    LevelBonusCap = 1.8
                },
                BossMultiplier = 1.2
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
    public void GetBiomeForStage_Stage101_ReturnsSwamp()
    {
        // Act
        var biome = _service.GetBiomeForStage(101);

        // Assert
        Assert.Equal("Swamp", biome);
    }

    [Fact]
    public void GetBiomeForStage_Stage200_ReturnsSwamp()
    {
        // Act
        var biome = _service.GetBiomeForStage(200);

        // Assert
        Assert.Equal("Swamp", biome);
    }

    [Fact]
    public void GetBiomeForStage_Stage901_ReturnsDark()
    {
        // Act
        var biome = _service.GetBiomeForStage(901);

        // Assert
        Assert.Equal("Dark", biome);
    }

    [Fact]
    public void GetBiomeForStage_Stage1000_ReturnsDark()
    {
        // Act
        var biome = _service.GetBiomeForStage(1000);

        // Assert
        Assert.Equal("Dark", biome);
    }

    [Fact]
    public void GetBiomeForStage_Stage1001_ReturnsVoid()
    {
        // Act
        var biome = _service.GetBiomeForStage(1001);

        // Assert
        Assert.Equal("Void", biome);
    }

    [Fact]
    public void GetBiomeForStage_Stage1500_ReturnsVoid()
    {
        // Act
        var biome = _service.GetBiomeForStage(1500);

        // Assert
        Assert.Equal("Void", biome);
    }

    [Fact]
    public void GetBiomeForStage_Stage99999_ReturnsVoid()
    {
        // Act
        var biome = _service.GetBiomeForStage(99999);

        // Assert
        Assert.Equal("Void", biome);
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
        // Offset in block: 3 % 10 = 3, rule 3-4 → count 2
        // Act
        var count = _service.GetEnemyCountForStage(3);

        // Assert
        Assert.Equal(2, count);
    }

    [Fact]
    public void GetEnemyCountForStage_Stage5_Returns3()
    {
        // Offset: 5 % 10 = 5, rule 5-6 → count 3
        // Act
        var count = _service.GetEnemyCountForStage(5);

        // Assert
        Assert.Equal(3, count);
    }

    [Fact]
    public void GetEnemyCountForStage_Stage7_Returns4()
    {
        // Offset: 7 % 10 = 7, rule 7-8 → count 4
        // Act
        var count = _service.GetEnemyCountForStage(7);

        // Assert
        Assert.Equal(4, count);
    }

    [Fact]
    public void GetEnemyCountForStage_Stage9_Returns5()
    {
        // Offset: 9 % 10 = 9, rule 9-9 → count 5
        // Act
        var count = _service.GetEnemyCountForStage(9);

        // Assert
        Assert.Equal(5, count);
    }

    [Fact]
    public void GetEnemyCountForStage_Stage11_Returns1()
    {
        // Offset in second block: 11 % 10 = 1, offset = 1 → count 1
        // Act
        var count = _service.GetEnemyCountForStage(11);

        // Assert
        Assert.Equal(1, count);
    }

    [Fact]
    public void GetEnemyCountForStage_Stage19_Returns5()
    {
        // Offset in second block: 19 % 10 = 9, rule 9-9 → count 5
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
    public void IsBossStage_Stage1000_ReturnsTrue()
    {
        // Act
        var isBoss = _service.IsBossStage(1000);

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
        // Unified scaling: curve = 1 + 0.12 * (2-1)^1.15 = 1.12
        // Act
        var (hp, damage) = _service.CalculateScaledStats(2, 100, 10, isBoss: false);

        // Assert
        Assert.True(hp > 100, "HP should scale up");
        Assert.True(damage >= 10, "Damage should scale up or stay same");
    }

    [Fact]
    public void CalculateScaledStats_Stage10_Boss_AppliesBossMultiplier()
    {
        // Boss multiplier (1.2x) should be applied on top of difficulty curve
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
    public void CalculateScaledStats_Stage50_ShowsPolynomialGrowth()
    {
        // Test that stats grow polynomially (faster than linear, slower than exponential)
        // Act
        var (hp10, _) = _service.CalculateScaledStats(10, 100, 10, isBoss: false);
        var (hp50, _) = _service.CalculateScaledStats(50, 100, 10, isBoss: false);

        // Assert
        // HP at stage 50 should be significantly higher than at stage 10 due to polynomial growth
        Assert.True(hp50 > hp10 * 3, $"Stage 50 HP ({hp50}) should be > 3x stage 10 HP ({hp10})");
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

    #region Unified Scaling Tests

    [Fact]
    public void GetUnifiedDifficultyCurve_Stage1_Returns1()
    {
        var curve = _service.GetUnifiedDifficultyCurve(1);
        Assert.Equal(1.0, curve);
    }

    [Fact]
    public void GetUnifiedDifficultyCurve_Stage10_ReturnsPolynomialValue()
    {
        // curve = 1 + 0.12 * (10 - 1)^1.15
        var curve = _service.GetUnifiedDifficultyCurve(10);
        Assert.True(curve > 1.0, "Curve should be > 1.0 at stage 10");
        Assert.True(curve < 5.0, $"Curve should be < 5.0 at stage 10, got {curve}");
    }

    [Fact]
    public void GetUnifiedDifficultyCurve_IsMonotonicallyIncreasing()
    {
        var curve10 = _service.GetUnifiedDifficultyCurve(10);
        var curve50 = _service.GetUnifiedDifficultyCurve(50);
        var curve100 = _service.GetUnifiedDifficultyCurve(100);
        var curve500 = _service.GetUnifiedDifficultyCurve(500);

        Assert.True(curve10 < curve50, "Curve should increase from stage 10 to 50");
        Assert.True(curve50 < curve100, "Curve should increase from stage 50 to 100");
        Assert.True(curve100 < curve500, "Curve should increase from stage 100 to 500");
    }

    [Fact]
    public void GetUnifiedRewardCurve_Stage1_Returns1()
    {
        var curve = _service.GetUnifiedRewardCurve(1);
        Assert.Equal(1.0, curve);
    }

    [Fact]
    public void GetUnifiedRewardCurve_IsMonotonicallyIncreasing()
    {
        var curve10 = _service.GetUnifiedRewardCurve(10);
        var curve100 = _service.GetUnifiedRewardCurve(100);

        Assert.True(curve10 > 1.0, "Reward curve should be > 1 at stage 10");
        Assert.True(curve100 > curve10, "Reward curve should increase from stage 10 to 100");
    }

    [Fact]
    public void GetRewardMultiplierForStage_ReturnsDefaultMultiplier()
    {
        // Forest biome has default reward multiplier (1.0)
        var mult = _service.GetRewardMultiplierForStage(1);
        Assert.Equal(1.0, mult);
    }

    [Fact]
    public void CalculateScaledStats_Unified_Stage1_ReturnsBaseStats()
    {
        var (hp, damage) = _service.CalculateScaledStats(1, 100, 10, isBoss: false);

        Assert.Equal(100, hp);
        Assert.Equal(10, damage);
    }

    [Fact]
    public void CalculateScaledStats_Unified_Stage50_ShowsGrowth()
    {
        var (hp1, _) = _service.CalculateScaledStats(1, 100, 10, isBoss: false);
        var (hp50, _) = _service.CalculateScaledStats(50, 100, 10, isBoss: false);

        Assert.True(hp50 > hp1 * 3, $"Stage 50 HP ({hp50}) should be > 3x stage 1 HP ({hp1})");
    }

    [Fact]
    public void CalculateScaledStats_Unified_Boss_AppliesBossMultiplier()
    {
        var (hpNormal, _) = _service.CalculateScaledStats(10, 100, 10, isBoss: false);
        var (hpBoss, _) = _service.CalculateScaledStats(10, 100, 10, isBoss: true);

        // Boss should have bossMultiplier (1.2) applied
        Assert.True(hpBoss > hpNormal, $"Boss HP ({hpBoss}) should be > normal HP ({hpNormal})");
    }

    #endregion
}
