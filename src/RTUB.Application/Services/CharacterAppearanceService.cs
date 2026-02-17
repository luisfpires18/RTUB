using Microsoft.Extensions.Logging;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing character appearance customization.
/// Handles CRUD for CharacterAppearance and builds sprite layer DTOs.
/// </summary>
public class CharacterAppearanceService : ICharacterAppearanceService
{
    private readonly IRepository<CharacterAppearance> _repository;
    private readonly ILogger<CharacterAppearanceService> _logger;

    private const string LayerBasePath = "/sprites/games/my-tuno/layers";

    public CharacterAppearanceService(
        IRepository<CharacterAppearance> repository,
        ILogger<CharacterAppearanceService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<CharacterAppearance> GetOrCreateAppearanceAsync(int characterId, CancellationToken cancellationToken = default)
    {
        if (characterId <= 0)
            throw new ArgumentException("Character ID must be positive", nameof(characterId));

        var appearance = await _repository.FirstOrDefaultAsync(a => a.CharacterId == characterId);
        if (appearance != null)
            return appearance;

        _logger.LogInformation("Creating default appearance for character {CharacterId}", characterId);
        appearance = CharacterAppearance.Create(characterId);
        return await _repository.AddAsync(appearance);
    }

    /// <inheritdoc />
    public async Task UpdateAppearanceAsync(
        int characterId,
        HairStyle hairStyle,
        string hairColor,
        string eyeColor,
        string skinColor,
        ClothesStyle clothesStyle,
        WeaponType? weaponVisual,
        CancellationToken cancellationToken = default)
    {
        if (characterId <= 0)
            throw new ArgumentException("Character ID must be positive", nameof(characterId));

        var appearance = await GetOrCreateAppearanceAsync(characterId, cancellationToken);
        appearance.UpdateAppearance(hairStyle, hairColor, eyeColor, skinColor, clothesStyle, weaponVisual);
        await _repository.UpdateAsync(appearance);

        _logger.LogInformation("Updated appearance for character {CharacterId}: Hair={HairStyle}, Clothes={ClothesStyle}",
            characterId, hairStyle, clothesStyle);
    }

    /// <inheritdoc />
    public CharacterSpriteLayers GetSpriteLayers(CharacterAppearance appearance)
    {
        ArgumentNullException.ThrowIfNull(appearance);

        return new CharacterSpriteLayers
        {
            BodyPath = $"{LayerBasePath}/body/base.png",
            BodyTint = appearance.SkinColor,
            EyesPath = $"{LayerBasePath}/eyes/base.png",
            EyesTint = appearance.EyeColor,
            HairPath = appearance.HairStyle != HairStyle.Bald
                ? $"{LayerBasePath}/hair/{GetHairFileName(appearance.HairStyle)}"
                : null,
            HairTint = appearance.HairColor,
            ClothesPath = $"{LayerBasePath}/clothes/{GetClothesFileName(appearance.ClothesStyle)}",
            WeaponPath = appearance.WeaponVisual.HasValue
                ? $"{LayerBasePath}/weapons/{GetWeaponFileName(appearance.WeaponVisual.Value)}"
                : null
        };
    }

    /// <inheritdoc />
    public async Task<CharacterSpriteLayers> GetSpriteLayersForCharacterAsync(int characterId, CancellationToken cancellationToken = default)
    {
        var appearance = await GetOrCreateAppearanceAsync(characterId, cancellationToken);
        return GetSpriteLayers(appearance);
    }

    /// <summary>
    /// Maps HairStyle enum to the corresponding PNG filename.
    /// </summary>
    private static string GetHairFileName(HairStyle style) => style switch
    {
        HairStyle.Short => "short.png",
        HairStyle.Long => "long.png",
        HairStyle.Spiky => "spiky.png",
        _ => "short.png"
    };

    /// <summary>
    /// Maps ClothesStyle enum to the corresponding PNG filename.
    /// </summary>
    private static string GetClothesFileName(ClothesStyle style) => style switch
    {
        ClothesStyle.Casual => "casual.png",
        ClothesStyle.Armor => "armor.png",
        ClothesStyle.Robe => "robe.png",
        _ => "casual.png"
    };

    /// <summary>
    /// Maps WeaponType enum to the corresponding PNG filename.
    /// </summary>
    private static string GetWeaponFileName(WeaponType type) => type switch
    {
        WeaponType.SwordOneHand => "sword_1h.png",
        WeaponType.AxeOneHand => "axe_1h.png",
        WeaponType.Mace => "mace.png",
        WeaponType.Shield => "shield.png",
        WeaponType.Dagger => "dagger.png",
        WeaponType.Staff => "staff.png",
        WeaponType.Bow => "bow.png",
        WeaponType.SwordTwoHand => "sword_2h.png",
        WeaponType.Spear => "spear.png",
        WeaponType.AxeTwoHand => "axe_2h.png",
        WeaponType.Hammer => "hammer.png",
        _ => "sword_1h.png"
    };
}
