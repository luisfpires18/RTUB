using System.ComponentModel.DataAnnotations;
using RTUB.Core.Enums;

namespace RTUB.Web.Pages.MyTuno.Models;

/// <summary>
/// Form model for creating/editing stage enemies.
/// Avoids issues with the entity's private constructor.
/// </summary>
public class StageEnemyFormModel
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public EnemyType Type { get; set; } = EnemyType.Normal;
    public RegionType Region { get; set; } = RegionType.Forest;
    public int BaseHP { get; set; } = 50;
    public int BasePower { get; set; } = 8;
    public int BaseSpeed { get; set; } = 5;
    public int BaseDefense { get; set; } = 3;
    public double BaseCriticalChance { get; set; } = 0.05;
    public decimal BaseFidelisDrop { get; set; } = 1.0m;
    public double FinoDropChance { get; set; } = 0.1;
    public double ShotDropChance { get; set; } = 0.05;

    [StringLength(500)]
    public string? SpritePath { get; set; }

    public int? BossStageNumber { get; set; }
    public PlacementType Placement { get; set; } = PlacementType.Terrestrial;
}
