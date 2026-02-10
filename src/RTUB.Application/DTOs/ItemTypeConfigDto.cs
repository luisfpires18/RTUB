namespace RTUB.Application.DTOs;

/// <summary>
/// DTO for item type configuration (weapons, drinks, equipment, forge combos).
/// Used in the WeaponDrinkConfig page.
/// </summary>
public class ItemTypeConfigDto
{
    public int Id { get; set; }
    public string TypeKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? PictureUrl { get; set; }
    public string IconClass { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}
