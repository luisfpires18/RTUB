using System.ComponentModel.DataAnnotations;
using RTUB.Core.Enums;

namespace RTUB.Core.Entities;

/// <summary>
/// Stores a character's visual customization choices.
/// One-to-one relationship with Character.
/// Sprite layers are composited at runtime using PixiJS tinting.
/// </summary>
public class CharacterAppearance : BaseEntity
{
    /// <summary>FK to the owning character</summary>
    [Required]
    public int CharacterId { get; set; }

    /// <summary>Selected hair style</summary>
    public HairStyle HairStyle { get; set; } = HairStyle.Short;

    /// <summary>Hair tint color as hex (e.g. "#3B2F2F")</summary>
    [Required]
    [MaxLength(7)]
    public string HairColor { get; set; } = "#3B2F2F";

    /// <summary>Eye tint color as hex (e.g. "#4A90D9")</summary>
    [Required]
    [MaxLength(7)]
    public string EyeColor { get; set; } = "#4A90D9";

    /// <summary>Skin tint color as hex (e.g. "#F5D6C3")</summary>
    [Required]
    [MaxLength(7)]
    public string SkinColor { get; set; } = "#F5D6C3";

    /// <summary>Selected clothing style</summary>
    public ClothesStyle ClothesStyle { get; set; } = ClothesStyle.Casual;

    /// <summary>Cosmetic weapon visual shown on the sprite (null = no weapon)</summary>
    public WeaponType? WeaponVisual { get; set; }

    // Navigation
    /// <summary>The character this appearance belongs to</summary>
    public virtual Character Character { get; set; } = null!;

    /// <summary>Private constructor for EF Core</summary>
    private CharacterAppearance() { }

    /// <summary>
    /// Factory method to create a default appearance for a character.
    /// </summary>
    /// <param name="characterId">The owning character's ID</param>
    public static CharacterAppearance Create(int characterId)
    {
        if (characterId <= 0)
            throw new ArgumentException("Character ID must be positive", nameof(characterId));

        return new CharacterAppearance
        {
            CharacterId = characterId
        };
    }

    /// <summary>
    /// Updates all appearance options at once.
    /// </summary>
    public void UpdateAppearance(
        HairStyle hairStyle,
        string hairColor,
        string eyeColor,
        string skinColor,
        ClothesStyle clothesStyle,
        WeaponType? weaponVisual)
    {
        ValidateHexColor(hairColor, nameof(hairColor));
        ValidateHexColor(eyeColor, nameof(eyeColor));
        ValidateHexColor(skinColor, nameof(skinColor));

        HairStyle = hairStyle;
        HairColor = hairColor;
        EyeColor = eyeColor;
        SkinColor = skinColor;
        ClothesStyle = clothesStyle;
        WeaponVisual = weaponVisual;
    }

    /// <summary>
    /// Validates that a string is a valid 7-character hex color (e.g. "#FF00AA").
    /// </summary>
    private static void ValidateHexColor(string color, string paramName)
    {
        if (string.IsNullOrWhiteSpace(color) || color.Length != 7 || color[0] != '#')
            throw new ArgumentException($"Color must be a 7-character hex string (e.g. \"#FF00AA\")", paramName);

        for (var i = 1; i < 7; i++)
        {
            if (!Uri.IsHexDigit(color[i]))
                throw new ArgumentException($"Color contains invalid hex character at position {i}", paramName);
        }
    }
}
