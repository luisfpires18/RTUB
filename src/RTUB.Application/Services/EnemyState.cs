using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Helper class to track enemy state during multi-enemy combat
/// </summary>
internal class EnemyState
{
    public Character Enemy { get; set; } = null!;
    public int Index { get; set; }
    public int HP { get; set; }
    public int MaxHP { get; set; }
    public string Name { get; set; } = string.Empty;
    public double ActionTimeMs { get; set; }
    public double Timer { get; set; }
}
