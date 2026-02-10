using Microsoft.Extensions.Logging;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Database-backed implementation of IItemTypeConfigService.
/// Uses IItemTypeConfigRepository for item configs and IForgeComboConfigRepository for forge combos.
/// Image uploads are stored in Cloudflare R2 via IItemTypeMediaStorageService.
/// Seed data is created on first use by ItemTypeConfigInitializer.
/// </summary>
public class ItemTypeConfigService : IItemTypeConfigService
{
    private readonly IItemTypeConfigRepository _configRepository;
    private readonly IForgeComboConfigRepository _forgeComboRepository;
    private readonly IItemTypeMediaStorageService _mediaStorageService;
    private readonly ILogger<ItemTypeConfigService> _logger;

    public ItemTypeConfigService(
        IItemTypeConfigRepository configRepository,
        IForgeComboConfigRepository forgeComboRepository,
        IItemTypeMediaStorageService mediaStorageService,
        ILogger<ItemTypeConfigService> logger)
    {
        _configRepository = configRepository;
        _forgeComboRepository = forgeComboRepository;
        _mediaStorageService = mediaStorageService;
        _logger = logger;
    }

    public async Task<List<ItemTypeConfigDto>> GetWeaponTypeConfigsAsync()
    {
        var configs = await _configRepository.GetByCategoryOrderedAsync("Weapon");
        return configs.Select(MapToDto).ToList();
    }

    public async Task<List<ItemTypeConfigDto>> GetDrinkTypeConfigsAsync()
    {
        var configs = await _configRepository.GetByCategoryOrderedAsync("Drink");
        return configs.Select(MapToDto).ToList();
    }

    public async Task<List<ItemTypeConfigDto>> GetEquipmentTypeConfigsAsync()
    {
        var configs = await _configRepository.GetByCategoryOrderedAsync("Equipment");
        return configs.Select(MapToDto).ToList();
    }

    public async Task<List<ItemTypeConfigDto>> GetAllConfigsAsync()
    {
        var configs = await _configRepository.GetAllOrderedAsync();
        return configs.Select(MapToDto).ToList();
    }

    public async Task UpdateConfigAsync(int id, string? pictureUrl)
    {
        var config = await _configRepository.GetByIdAsync(id);
        if (config == null)
        {
            _logger.LogWarning("Attempted to update unknown config ID: {Id}", id);
            return;
        }

        config.Update(pictureUrl);
        await _configRepository.UpdateAsync(config);
    }

    public async Task<string?> UploadConfigPictureAsync(int id, Stream imageStream, string fileName, string contentType)
    {
        var config = await _configRepository.GetByIdAsync(id);
        if (config == null)
        {
            _logger.LogWarning("Attempted to upload picture for unknown config ID: {Id}", id);
            return null;
        }

        // Delete old image if exists
        if (!string.IsNullOrEmpty(config.PictureUrl))
        {
            await _mediaStorageService.DeleteImageAsync(config.PictureUrl);
        }

        var url = await _mediaStorageService.UploadImageAsync(imageStream, fileName, contentType, config.TypeKey);
        config.Update(url);
        await _configRepository.UpdateAsync(config);
        return url;
    }

    public async Task RemoveConfigPictureAsync(int id)
    {
        var config = await _configRepository.GetByIdAsync(id);
        if (config == null) return;

        if (!string.IsNullOrEmpty(config.PictureUrl))
        {
            await _mediaStorageService.DeleteImageAsync(config.PictureUrl);
            config.Update(null);
            await _configRepository.UpdateAsync(config);
        }
    }

    public string GetImageUrl(string? imageSrc, int refreshTrigger)
    {
        if (string.IsNullOrEmpty(imageSrc)) return string.Empty;
        return refreshTrigger > 0 ? $"{imageSrc}?v={refreshTrigger}" : imageSrc;
    }

    // ── Forge Combo Methods ──

    public async Task<Dictionary<string, string>> GetAllForgeComboPicturesAsync()
    {
        var combos = await _forgeComboRepository.GetAllWithPicturesAsync();
        return combos.ToDictionary(c => c.ComboKey, c => c.PictureUrl!);
    }

    public async Task<string?> UploadForgeComboPictureAsync(string comboKey, Stream imageStream, string fileName, string contentType)
    {
        var config = await _forgeComboRepository.GetByComboKeyAsync(comboKey);

        // Delete old image if exists
        if (config != null && !string.IsNullOrEmpty(config.PictureUrl))
        {
            await _mediaStorageService.DeleteImageAsync(config.PictureUrl);
        }

        var url = await _mediaStorageService.UploadImageAsync(imageStream, fileName, contentType, $"forge_{comboKey}");

        if (config == null)
        {
            config = ForgeComboConfig.Create(comboKey, url);
            await _forgeComboRepository.AddAsync(config);
        }
        else
        {
            config.UpdatePicture(url);
            await _forgeComboRepository.UpdateAsync(config);
        }

        return url;
    }

    public async Task RemoveForgeComboPictureAsync(string comboKey)
    {
        var config = await _forgeComboRepository.GetByComboKeyAsync(comboKey);
        if (config == null) return;

        if (!string.IsNullOrEmpty(config.PictureUrl))
        {
            await _mediaStorageService.DeleteImageAsync(config.PictureUrl);
            config.UpdatePicture(null);
            await _forgeComboRepository.UpdateAsync(config);
        }
    }

    private static ItemTypeConfigDto MapToDto(ItemTypeConfig config) => new()
    {
        Id = config.Id,
        TypeKey = config.TypeKey,
        DisplayName = config.DisplayName,
        PictureUrl = config.PictureUrl,
        IconClass = config.IconClass,
        Category = config.Category
    };
}
