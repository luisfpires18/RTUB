namespace RTUB.Application.DTOs;

/// <summary>
/// DTO containing all sprite layer paths and tint colors for client-side compositing.
/// Serialized to JSON and passed to PixiJS battle modules.
/// Tintable layers use grayscale PNGs with runtime color application.
/// </summary>
public class CharacterSpriteLayers
{
    /// <summary>Path to the body base sprite (grayscale, tinted by SkinTint)</summary>
    public string BodyPath { get; set; } = "/sprites/games/my-tuno/layers/body/base.png";

    /// <summary>Hex tint color for the body sprite</summary>
    public string BodyTint { get; set; } = "#F5D6C3";

    /// <summary>Path to the eyes sprite (grayscale, tinted by EyesTint)</summary>
    public string EyesPath { get; set; } = "/sprites/games/my-tuno/layers/eyes/base.png";

    /// <summary>Hex tint color for the eyes sprite</summary>
    public string EyesTint { get; set; } = "#4A90D9";

    /// <summary>Path to the hair sprite (grayscale, tinted by HairTint). Null if bald.</summary>
    public string? HairPath { get; set; }

    /// <summary>Hex tint color for the hair sprite</summary>
    public string HairTint { get; set; } = "#3B2F2F";

    /// <summary>Path to the clothes sprite (full-color, no tint)</summary>
    public string ClothesPath { get; set; } = "/sprites/games/my-tuno/layers/clothes/casual.png";

    /// <summary>Path to the weapon sprite (full-color, no tint). Null if no weapon.</summary>
    public string? WeaponPath { get; set; }
}
