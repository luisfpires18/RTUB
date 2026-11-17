// Naipe: instrument type configuration (cache, pictures, push).
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RTUB.Application.DTOs;
using RTUB.Application.Extensions;
using RTUB.Application.Helpers;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

public partial class NaipeService
{
    // ========================
    // Naipe Type Configuration Methods
    // ========================

    public async Task<List<NaipeTypeConfigDto>> GetAllTypeConfigsAsync()
    {
        return await _cache.GetOrCreateAsync(AllTypeConfigsCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            var configs = await _naipeTypeConfigRepository.GetAllOrderedAsync();
            return configs.Select(MapTypeConfigToDto).ToList();
        }) ?? new List<NaipeTypeConfigDto>();
    }

    public async Task<List<NaipeTypeConfigDto>> GetVisibleTypeConfigsAsync()
    {
        var configs = await _naipeTypeConfigRepository.GetVisibleOrderedAsync();
        return configs.Select(MapTypeConfigToDto).ToList();
    }

    public async Task InitializeTypeConfigsAsync()
    {
        // Check if any configs exist
        var existingConfigs = await _naipeTypeConfigRepository.GetAllOrderedAsync();
        var existingTypes = existingConfigs.Select(c => c.InstrumentType).ToHashSet();

        // Create configs for any missing types
        var allTypes = Enum.GetValues<InstrumentType>();
        var sortOrder = existingConfigs.Any() ? existingConfigs.Max(c => c.SortOrder) + 1 : 0;

        foreach (var type in allTypes)
        {
            if (!existingTypes.Contains(type))
            {
                var config = NaipeTypeConfig.Create(type, sortOrder++);
                await _naipeTypeConfigRepository.AddAsync(config);
            }
        }
    }

    public async Task UpdateTypeConfigAsync(int id, string? pictureUrl, bool isVisible, int sortOrder)
    {
        var config = await _naipeTypeConfigRepository.GetByIdOrThrowAsync(id);

        config.Update(pictureUrl, isVisible, sortOrder);
        await _naipeTypeConfigRepository.UpdateAsync(config);
        _cache.Remove(AllTypeConfigsCacheKey);
    }

    public async Task<string> UploadTypeConfigPictureAsync(int id, Stream fileStream, string fileName, string contentType)
    {
        var config = await _naipeTypeConfigRepository.GetByIdOrThrowAsync(id);

        // Upload image to Cloudflare R2
        var url = await _naipeMediaStorageService.UploadImageAsync(fileStream, fileName, contentType, $"typeconfig_{config.InstrumentType}");

        // Delete old picture if exists
        if (!string.IsNullOrEmpty(config.PictureUrl))
        {
            await _naipeMediaStorageService.DeleteMediaAsync(config.PictureUrl);
        }

        config.Update(url, config.IsVisible, config.SortOrder);
        await _naipeTypeConfigRepository.UpdateAsync(config);
        _cache.Remove(AllTypeConfigsCacheKey);

        return url;
    }

    private NaipeTypeConfigDto MapTypeConfigToDto(NaipeTypeConfig config)
    {
        return new NaipeTypeConfigDto
        {
            Id = config.Id,
            InstrumentType = config.InstrumentType,
            InstrumentTypeName = StatusHelper.GetInstrumentDisplay(config.InstrumentType),
            PictureUrl = config.PictureUrl,
            IsVisible = config.IsVisible,
            SortOrder = config.SortOrder
        };
    }

    /// <summary>
    /// Sends a push notification to all subscribed users when new naipe content is created.
    /// </summary>
    private async Task SendNaipeContentPushNotificationAsync(string contentTitle, InstrumentType instrumentType, bool isVideo)
    {
        try
        {
            var notification = _pushNotificationFactory.CreateNaipeContentNotification(
                contentTitle,
                instrumentType,
                isVideo
            );

            await _pushNotificationService.BroadcastAsync(notification);
        }
        catch
        {
            // Silently fail - push notifications should not break content creation
        }
    }
}
