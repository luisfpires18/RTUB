using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents configuration for an item type (weapon, drink, or equipment) in My Tuno.
/// Allows admins to configure picture per item type.
/// </summary>
public class ItemTypeConfig : BaseEntity
{
    /// <summary>
    /// Unique key identifying the item type (e.g., "SwordOneHand", "Cerveja", "EquipmentHead").
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string TypeKey { get; set; } = string.Empty;

    /// <summary>
    /// Display name shown in the UI.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Optional picture URL for the item type card.
    /// </summary>
    [MaxLength(2048)]
    public string? PictureUrl { get; set; }

    /// <summary>
    /// Bootstrap icon CSS class for fallback display.
    /// </summary>
    [MaxLength(100)]
    public string IconClass { get; set; } = string.Empty;

    /// <summary>
    /// Category grouping: "Weapon", "Drink", or "Equipment".
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    // Private constructor for EF Core
    private ItemTypeConfig() { }

    /// <summary>
    /// Factory method for creating a new ItemTypeConfig.
    /// </summary>
    public static ItemTypeConfig Create(
        string typeKey,
        string displayName,
        string iconClass,
        string category,
        string? pictureUrl = null)
    {
        return new ItemTypeConfig
        {
            TypeKey = typeKey,
            DisplayName = displayName,
            IconClass = iconClass,
            Category = category,
            PictureUrl = pictureUrl,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Update the configuration.
    /// </summary>
    public void Update(string? pictureUrl)
    {
        PictureUrl = pictureUrl;
        UpdatedAt = DateTime.UtcNow;
    }
}
