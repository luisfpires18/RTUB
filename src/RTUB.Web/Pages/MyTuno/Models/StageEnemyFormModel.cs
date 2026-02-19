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

    [StringLength(500)]
    public string? SpritePath { get; set; }

    public int? BossStageNumber { get; set; }
    public PlacementType Placement { get; set; } = PlacementType.Terrestrial;
}
