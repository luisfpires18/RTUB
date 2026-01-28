using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a player's character in My Tuno
/// Main persistent entity for the game
/// </summary>
public class Character : BaseEntity
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    // Progression
    public int Level { get; set; } = 1;
    public int XP { get; set; } = 0;

    // Base Stats (ONLY these three)
    public int HP { get; set; } = 100;        // Base HP
    public int Power { get; set; } = 10;      // Base Power
    public int Speed { get; set; } = 10;      // Base Speed

    // Upgrade Counts (for cost calculation)
    public int HpUpgrades { get; set; } = 0;
    public int PowerUpgrades { get; set; } = 0;
    public int SpeedUpgrades { get; set; } = 0;

    // Navigation
    public virtual ApplicationUser User { get; set; } = null!;

    // Computed properties (not stored in database)
    // Stats scale with level: base stats increase by 10% per level
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int TotalHP => (int)(HP * (1 + (Level - 1) * 0.1)) + (HpUpgrades * 10);      // Base HP scales with level, +10 HP per upgrade

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int TotalPower => (int)(Power * (1 + (Level - 1) * 0.1)) + (PowerUpgrades * 2);  // Base Power scales with level, +2 Power per upgrade

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int TotalSpeed => (int)(Speed * (1 + (Level - 1) * 0.1)) + (SpeedUpgrades * 1);  // Base Speed scales with level, +1 Speed per upgrade

    // Private constructor for EF Core
    private Character() { }

    /// <summary>
    /// Factory method to create a new character for a user
    /// </summary>
    public static Character Create(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required", nameof(userId));

        return new Character
        {
            UserId = userId,
            Level = 1,
            XP = 0,
            HP = 100,
            Power = 10,
            Speed = 10,
            HpUpgrades = 0,
            PowerUpgrades = 0,
            SpeedUpgrades = 0
        };
    }

    /// <summary>
    /// Adds XP to the character and handles level-ups
    /// Level up formula: Each level requires 100 * level XP to reach the next level
    /// Level 1->2: 100 XP, Level 2->3: 200 XP, Level 3->4: 300 XP, etc.
    /// </summary>
    public void AddXP(int amount)
    {
        if (amount < 0)
            throw new ArgumentException("XP amount cannot be negative", nameof(amount));

        XP += amount;

        // Level up logic: Each level requires (Level * 100) XP to reach the next level
        // Level 1 needs 100 XP to become Level 2
        // Level 2 needs 200 XP to become Level 3
        // Level 3 needs 300 XP to become Level 4, etc.
        while (XP >= Level * 100)
        {
            XP -= Level * 100;
            Level++;
        }
    }

    /// <summary>
    /// Upgrades HP stat (increments HpUpgrades count)
    /// </summary>
    public void UpgradeHP()
    {
        HpUpgrades++;
    }

    /// <summary>
    /// Upgrades Power stat (increments PowerUpgrades count)
    /// </summary>
    public void UpgradePower()
    {
        PowerUpgrades++;
    }

    /// <summary>
    /// Upgrades Speed stat (increments SpeedUpgrades count)
    /// </summary>
    public void UpgradeSpeed()
    {
        SpeedUpgrades++;
    }
}
