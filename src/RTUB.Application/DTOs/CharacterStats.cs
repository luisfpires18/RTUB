namespace RTUB.Application.DTOs;

/// <summary>
/// DTO for character stats with equipment bonuses
/// </summary>
public class CharacterStats
{
    public int HP { get; set; }
    public int Power { get; set; }
    public int Speed { get; set; }
    public double CriticalChance { get; set; }
}
