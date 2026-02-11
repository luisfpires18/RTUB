using Microsoft.Extensions.Caching.Memory;
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
/// Caches GetAllConfigsAsync and GetAllForgeComboPicturesAsync results since these rarely change.
/// </summary>
public class ItemTypeConfigService : IItemTypeConfigService
{
    private readonly IItemTypeConfigRepository _configRepository;
    private readonly IForgeComboConfigRepository _forgeComboRepository;
    private readonly IItemTypeMediaStorageService _mediaStorageService;
    private readonly ILogger<ItemTypeConfigService> _logger;
    private readonly IMemoryCache _cache;

    private const string AllConfigsCacheKey = "ItemTypeConfig_All";
    private const string ForgeComboPicturesCacheKey = "ForgeCombo_Pictures";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    public ItemTypeConfigService(
        IItemTypeConfigRepository configRepository,
        IForgeComboConfigRepository forgeComboRepository,
        IItemTypeMediaStorageService mediaStorageService,
        ILogger<ItemTypeConfigService> logger,
        IMemoryCache cache)
    {
        _configRepository = configRepository;
        _forgeComboRepository = forgeComboRepository;
        _mediaStorageService = mediaStorageService;
        _logger = logger;
        _cache = cache;
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

    public async Task<List<ItemTypeConfigDto>> GetConsumableTypeConfigsAsync()
    {
        var configs = await _configRepository.GetByCategoryOrderedAsync("Consumable");
        return configs.Select(MapToDto).ToList();
    }

    public async Task<List<ItemTypeConfigDto>> GetAllConfigsAsync()
    {
        return await _cache.GetOrCreateAsync(AllConfigsCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            var configs = await _configRepository.GetAllOrderedAsync();
            return configs.Select(MapToDto).ToList();
        }) ?? new List<ItemTypeConfigDto>();
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
        _cache.Remove(AllConfigsCacheKey);
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
        _cache.Remove(AllConfigsCacheKey);
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
            _cache.Remove(AllConfigsCacheKey);
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
        return await _cache.GetOrCreateAsync(ForgeComboPicturesCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            var combos = await _forgeComboRepository.GetAllWithPicturesAsync();
            return combos.ToDictionary(c => c.ComboKey, c => c.PictureUrl!);
        }) ?? new Dictionary<string, string>();
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

        _cache.Remove(ForgeComboPicturesCacheKey);
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
            _cache.Remove(ForgeComboPicturesCacheKey);
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
