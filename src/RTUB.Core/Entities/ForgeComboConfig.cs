using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents configuration for a forge combination image in My Tuno.
/// Key format: "{WeaponType}_{InstrumentPart}_{Drink}" (e.g., "SwordOneHand_GuitarraPart_Cerveja").
/// </summary>
public class ForgeComboConfig : BaseEntity
{
    /// <summary>
    /// Unique combination key identifying the forge recipe.
    /// Format: "{WeaponType}_{InstrumentPart}_{Drink}".
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string ComboKey { get; set; } = string.Empty;

    /// <summary>
    /// Picture URL for this specific forge combination.
    /// </summary>
    [MaxLength(2048)]
    public string? PictureUrl { get; set; }

    // Private constructor for EF Core
    private ForgeComboConfig() { }

    /// <summary>
    /// Factory method for creating a new ForgeComboConfig.
    /// </summary>
    public static ForgeComboConfig Create(string comboKey, string? pictureUrl = null)
    {
        return new ForgeComboConfig
        {
            ComboKey = comboKey,
            PictureUrl = pictureUrl,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Update the picture URL.
    /// </summary>
    public void UpdatePicture(string? pictureUrl)
    {
        PictureUrl = pictureUrl;
        UpdatedAt = DateTime.UtcNow;
    }
}
