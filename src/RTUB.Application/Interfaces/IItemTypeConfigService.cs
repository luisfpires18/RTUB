using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for weapon type, drink type, equipment, and forge combo configuration.
/// All data is persisted in ItemTypeConfig and ForgeComboConfig database tables.
/// </summary>
public interface IItemTypeConfigService
{
    /// <summary>
    /// Gets all weapon type configurations ordered by sort order.
    /// </summary>
    Task<List<ItemTypeConfigDto>> GetWeaponTypeConfigsAsync();

    /// <summary>
    /// Gets all drink type configurations ordered by sort order.
    /// </summary>
    Task<List<ItemTypeConfigDto>> GetDrinkTypeConfigsAsync();

    /// <summary>
    /// Gets all equipment type configurations ordered by sort order.
    /// </summary>
    Task<List<ItemTypeConfigDto>> GetEquipmentTypeConfigsAsync();

    /// <summary>
    /// Gets all consumable type configurations ordered by sort order.
    /// </summary>
    Task<List<ItemTypeConfigDto>> GetConsumableTypeConfigsAsync();

    /// <summary>
    /// Gets all configurations (all categories) as a list.
    /// </summary>
    Task<List<ItemTypeConfigDto>> GetAllConfigsAsync();

    /// <summary>
    /// Updates a configuration entry picture.
    /// </summary>
    Task UpdateConfigAsync(int id, string? pictureUrl);

    /// <summary>
    /// Uploads a picture for a configuration entry and returns the picture URL.
    /// </summary>
    Task<string?> UploadConfigPictureAsync(int id, Stream imageStream, string fileName, string contentType);

    /// <summary>
    /// Removes the picture from a configuration entry.
    /// </summary>
    Task RemoveConfigPictureAsync(int id);

    /// <summary>
    /// Gets the full image URL for display, with cache-busting.
    /// </summary>
    string GetImageUrl(string? imageSrc, int refreshTrigger);

    // ── Forge Combo Methods ──

    /// <summary>
    /// Gets all forge combo pictures as a dictionary (comboKey → URL).
    /// </summary>
    Task<Dictionary<string, string>> GetAllForgeComboPicturesAsync();

    /// <summary>
    /// Uploads a picture for a forge combination and returns the URL.
    /// </summary>
    Task<string?> UploadForgeComboPictureAsync(string comboKey, Stream imageStream, string fileName, string contentType);

    /// <summary>
    /// Removes the picture from a forge combination.
    /// </summary>
    Task RemoveForgeComboPictureAsync(string comboKey);
}
