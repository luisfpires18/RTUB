using RTUB.Application.DTOs;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for character appearance customization.
/// Manages visual sprite layer choices for My Tuno characters.
/// </summary>
public interface ICharacterAppearanceService
{
    /// <summary>
    /// Gets or creates a default appearance for a character.
    /// </summary>
    /// <param name="characterId">The character ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The appearance (existing or newly created with defaults)</returns>
    Task<CharacterAppearance> GetOrCreateAppearanceAsync(int characterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates all appearance options for a character.
    /// </summary>
    /// <param name="characterId">The character ID</param>
    /// <param name="hairStyle">Selected hair style</param>
    /// <param name="hairColor">Hair tint hex color (e.g. "#3B2F2F")</param>
    /// <param name="eyeColor">Eye tint hex color</param>
    /// <param name="skinColor">Skin tint hex color</param>
    /// <param name="clothesStyle">Selected clothing style</param>
    /// <param name="weaponVisual">Cosmetic weapon visual (null for none)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UpdateAppearanceAsync(
        int characterId,
        HairStyle hairStyle,
        string hairColor,
        string eyeColor,
        string skinColor,
        ClothesStyle clothesStyle,
        WeaponType? weaponVisual,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the sprite layer paths and tint colors DTO for client-side compositing.
    /// </summary>
    /// <param name="appearance">The character's appearance entity</param>
    /// <returns>DTO with all layer paths and tint colors</returns>
    CharacterSpriteLayers GetSpriteLayers(CharacterAppearance appearance);

    /// <summary>
    /// Gets sprite layers for a character, loading or creating the appearance if needed.
    /// Convenience method that combines GetOrCreateAppearanceAsync + GetSpriteLayers.
    /// </summary>
    /// <param name="characterId">The character ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>DTO with all layer paths and tint colors</returns>
    Task<CharacterSpriteLayers> GetSpriteLayersForCharacterAsync(int characterId, CancellationToken cancellationToken = default);
}
