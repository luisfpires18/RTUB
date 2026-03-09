namespace RTUB.Application.Configuration;

/// <summary>
/// Configuration for the BMR - Bebe mais Rui platformer game
/// </summary>
public class BmrBebeMaisRuiConfiguration
{
    public const string SectionName = "Games:BmrBebeMaisRui";

    /// <summary>
    /// Starting health/lives for the player
    /// </summary>
    public int StartingHealth { get; set; } = 100;

    /// <summary>
    /// Duration of invulnerability after taking damage (milliseconds)
    /// </summary>
    public int InvulnerabilityMs { get; set; } = 1500;

    /// <summary>
    /// Jump strength (vertical velocity)
    /// </summary>
    public double JumpStrength { get; set; } = 650;

    /// <summary>
    /// Gravity force applied to player
    /// </summary>
    public double Gravity { get; set; } = 1200;

    /// <summary>
    /// Player horizontal movement speed
    /// </summary>
    public double MoveSpeed { get; set; } = 200;

    /// <summary>
    /// Points awarded per beer collected
    /// </summary>
    public int BeerPoints { get; set; } = 10;

    /// <summary>
    /// Time in seconds to level up (distance/time based progression)
    /// </summary>
    public int LevelUpSeconds { get; set; } = 30;

    /// <summary>
    /// Difficulty scaling configuration
    /// </summary>
    public DifficultyScalingConfig DifficultyScaling { get; set; } = new();

    /// <summary>
    /// Enemy tier definitions
    /// </summary>
    public List<EnemyTierConfig> EnemyTiers { get; set; } = new()
    {
        new EnemyTierConfig { Name = "Chubby", Damage = 10, Speed = 60, SpawnWeight = 40, SpritePath = "/sprites/games/bmr/enemy_chubby.svg" },
        new EnemyTierConfig { Name = "Plus", Damage = 20, Speed = 50, SpawnWeight = 30, SpritePath = "/sprites/games/bmr/enemy_plus.svg" },
        new EnemyTierConfig { Name = "Heavy", Damage = 35, Speed = 40, SpawnWeight = 20, SpritePath = "/sprites/games/bmr/enemy_heavy.svg" },
        new EnemyTierConfig { Name = "Mega", Damage = 50, Speed = 30, SpawnWeight = 10, SpritePath = "/sprites/games/bmr/enemy_mega.svg" }
    };
}

