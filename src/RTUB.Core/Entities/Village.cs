using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a player's village in My Tuno Village.
/// One village per user. Contains resources, buildings, fields, troops, and missions.
/// </summary>
public class Village : BaseEntity
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Village name / region.
    /// </summary>
    public string Kingdom { get; set; } = "Vila";

    /// <summary>
    /// Village X position on the world map.
    /// </summary>
    public int PosX { get; set; } = 0;

    /// <summary>
    /// Village Y position on the world map.
    /// </summary>
    public int PosY { get; set; } = 0;

    /// <summary>
    /// Last time resources were calculated/ticked.
    /// Used for delta-based resource accrual.
    /// </summary>
    public DateTime LastTick { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Current wood stockpile (double to handle fractional production).
    /// </summary>
    public double Wood { get; set; } = 200;

    /// <summary>
    /// Current stone stockpile.
    /// </summary>
    public double Stone { get; set; } = 200;

    /// <summary>
    /// Current food stockpile.
    /// </summary>
    public double Food { get; set; } = 150;

    // Navigation properties
    public virtual ApplicationUser User { get; set; } = null!;
    public virtual ICollection<VillageField> Fields { get; set; } = new List<VillageField>();
    public virtual ICollection<VillageBuilding> Buildings { get; set; } = new List<VillageBuilding>();
    public virtual ICollection<VillageTroop> Troops { get; set; } = new List<VillageTroop>();
    public virtual ICollection<VillageMission> Missions { get; set; } = new List<VillageMission>();
    public virtual VillageBuildJob? ActiveBuildJob { get; set; }

    // Private constructor for EF Core
    private Village() { }

    /// <summary>
    /// Factory method to create a new village for a user.
    /// </summary>
    public static Village Create(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required", nameof(userId));

        return new Village
        {
            UserId = userId,
            Kingdom = "Vila",
            PosX = 0,
            PosY = 0,
            LastTick = DateTime.UtcNow,
            Wood = 200,
            Stone = 200,
            Food = 150
        };
    }

    /// <summary>
    /// Ticks resource production based on time elapsed since last tick.
    /// Returns the delta seconds applied.
    /// </summary>
    public double TickResources(double woodRate, double stoneRate, double foodRate, double storageCap)
    {
        var now = DateTime.UtcNow;
        var deltaSecs = (now - LastTick).TotalSeconds;
        if (deltaSecs <= 0) return 0;

        Wood = Math.Min(storageCap, Wood + woodRate * deltaSecs);
        Stone = Math.Min(storageCap, Stone + stoneRate * deltaSecs);
        Food = Math.Min(storageCap, Food + foodRate * deltaSecs);

        LastTick = now;
        return deltaSecs;
    }

    /// <summary>
    /// Checks if the village can afford a resource cost.
    /// </summary>
    public bool CanAfford(double wood, double stone, double food)
    {
        return Wood >= wood && Stone >= stone && Food >= food;
    }

    /// <summary>
    /// Deducts resources. Returns false if insufficient.
    /// </summary>
    public bool SpendResources(double wood, double stone, double food)
    {
        if (!CanAfford(wood, stone, food))
            return false;

        Wood -= wood;
        Stone -= stone;
        Food -= food;
        return true;
    }

    /// <summary>
    /// Adds resources (e.g., mission rewards), clamped to storage cap.
    /// </summary>
    public void AddResources(double wood, double stone, double food, double storageCap)
    {
        Wood = Math.Min(storageCap, Wood + wood);
        Stone = Math.Min(storageCap, Stone + stone);
        Food = Math.Min(storageCap, Food + food);
    }
}
