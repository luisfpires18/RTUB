using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing naipe educational content using Repository pattern
/// Handles videos and images organized by instrument type
/// </summary>
public partial class NaipeService : INaipeService
{
    private readonly INaipeContentRepository _naipeContentRepository;

    private readonly INaipeCommentRepository _naipeCommentRepository;

    private readonly INaipeTypeConfigRepository _naipeTypeConfigRepository;

    private readonly INaipeMediaStorageService _naipeMediaStorageService;

    private readonly IPushNotificationService _pushNotificationService;

    private readonly IPushNotificationFactory _pushNotificationFactory;

    private readonly UserManager<ApplicationUser> _userManager;

    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    private readonly AuditContext _auditContext;

    private readonly IHttpContextAccessor _httpContextAccessor;

    private readonly IMemoryCache _cache;

    private const string AllTypeConfigsCacheKey = "NaipeTypeConfig_All";

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    public NaipeService(
        INaipeContentRepository naipeContentRepository,
        INaipeCommentRepository naipeCommentRepository,
        INaipeTypeConfigRepository naipeTypeConfigRepository,
        INaipeMediaStorageService naipeMediaStorageService,
        IPushNotificationService pushNotificationService,
        IPushNotificationFactory pushNotificationFactory,
        UserManager<ApplicationUser> userManager,
        IDbContextFactory<ApplicationDbContext> contextFactory,
        AuditContext auditContext,
        IHttpContextAccessor httpContextAccessor,
        IMemoryCache cache)
    {
        _naipeContentRepository = naipeContentRepository;
        _naipeCommentRepository = naipeCommentRepository;
        _naipeTypeConfigRepository = naipeTypeConfigRepository;
        _naipeMediaStorageService = naipeMediaStorageService;
        _pushNotificationService = pushNotificationService;
        _pushNotificationFactory = pushNotificationFactory;
        _userManager = userManager;
        _contextFactory = contextFactory;
        _auditContext = auditContext;
        _httpContextAccessor = httpContextAccessor;
        _cache = cache;
    }

    public async Task<List<NaipeContentDto>> GetContentByInstrumentTypeAsync(InstrumentType type)
    {
        var contents = await _naipeContentRepository.GetContentByInstrumentTypeAsync(type);
        return contents.Select(MapToDto).ToList();
    }

    public async Task<List<NaipeContentDto>> GetAllContentAsync()
    {
        var contents = await _naipeContentRepository.GetAllContentWithDetailsAsync();
        return contents.Select(MapToDto).ToList();
    }

    public async Task<NaipeContentDto?> GetContentByIdAsync(int id)
    {
        var content = await _naipeContentRepository.GetByIdWithDetailsAsync(id);
        return content == null ? null : MapToDto(content);
    }

    public async Task<NaipeContentDto> CreateContentAsync(InstrumentType type, string title, string? description, Stream fileStream, string fileName, string mimeType, bool isVideo, decimal sortOrder, string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            throw new EntityNotFoundException(nameof(ApplicationUser), userId);

        // Any logged-in user can create content

        // Upload file to Cloudflare R2
        string url;
        if (isVideo)
        {
            url = await _naipeMediaStorageService.UploadVideoAsync(fileStream, fileName, mimeType, type.ToString());
        }
        else
        {
            url = await _naipeMediaStorageService.UploadImageAsync(fileStream, fileName, mimeType, type.ToString());
        }

        var content = NaipeContent.Create(type, title, url, mimeType, isVideo, sortOrder, userId, description);
        // The automatic audit entry ("Created", with the fields) is the record; no second, manual one.
        var createdContent = await _naipeContentRepository.AddAsync(content);

        // Send push notification to all subscribed users
        await SendNaipeContentPushNotificationAsync(title, type, isVideo);

        return MapToDto(createdContent);
    }

    public async Task UpdateContentAsync(int id, string title, string? description, decimal sortOrder, string userId, bool isAdmin)
    {
        var content = await _naipeContentRepository.GetByIdOrThrowAsync(id);

        // Only owner or admin can update content
        var isOwner = content.CreatedByUserId == userId;

        if (!isOwner && !isAdmin)
            throw new UnauthorizedAccessException("Only the content owner or administrators can update naipe content.");

        // The automatic audit entry ("Modified", old -> new) is the record; no second, manual one.
        content.Update(title, description, sortOrder);
        await _naipeContentRepository.UpdateAsync(content);
    }

    public async Task DeleteContentAsync(int id, string userId, bool isAdmin)
    {
        var content = await _naipeContentRepository.GetByIdOrThrowAsync(id);

        // Only owner or admin can delete content
        var isOwner = content.CreatedByUserId == userId;

        if (!isOwner && !isAdmin)
            throw new UnauthorizedAccessException("Only the content owner or administrators can delete naipe content.");

        // Delete media from Cloudflare R2
        await _naipeMediaStorageService.DeleteMediaAsync(content.Url);

        // The automatic audit entry ("Deleted", with the fields) is the record; no second, manual one.
        await _naipeContentRepository.DeleteAsync(content);
    }

    public async Task IncrementPlayCountAsync(int contentId, string? userId)
    {
        var content = await _naipeContentRepository.GetByIdOrThrowAsync(contentId);

        var playCount = new NaipePlayCount
        {
            NaipeContentId = contentId,
            UserId = userId,
            PlayedAt = DateTime.UtcNow
        };

        using (var context = _contextFactory.CreateDbContext())
        {
            context.NaipePlayCounts.Add(playCount);
            await context.SaveChangesAsync();
        }

        // Create audit log (only if user is authenticated to avoid spam)
        if (!string.IsNullOrEmpty(userId))
        {
            var user = await _userManager.FindByIdAsync(userId);
            await CreateAuditLogAsync(
                content.IsVideo ? "Video Played" : "Image Viewed",
                content.Id,
                content.Title,
                $"{(content.IsVideo ? "Video" : "Image")} '{content.Title}' for {content.InstrumentType} {(content.IsVideo ? "played" : "viewed")} by {user?.Nickname ?? user?.UserName ?? "Unknown"}",
                user?.Id,
                user?.UserName
            );
        }
    }

    public async Task<int> GetPlayCountAsync(int contentId)
    {
        return await _naipeContentRepository.GetPlayCountAsync(contentId);
    }
}
