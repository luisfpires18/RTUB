namespace RTUB.Core.Entities;

public class Character : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public int Level { get; set; } = 1;
    public int Xp { get; set; }

    public int Hp { get; set; } = 10;
    public int Power { get; set; } = 5;
    public int Speed { get; set; } = 5;

    public int HpUpgrades { get; set; }
    public int PowerUpgrades { get; set; }
    public int SpeedUpgrades { get; set; }
}
