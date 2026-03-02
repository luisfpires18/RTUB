using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Extensions;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing naipe educational content using Repository pattern
/// Handles videos and images organized by instrument type
/// </summary>
public class NaipeService : INaipeService
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
        var createdContent = await _naipeContentRepository.AddAsync(content);

        // Create audit log
        await CreateAuditLogAsync(
            isVideo ? "Video Created" : "Image Created",
            createdContent.Id,
            createdContent.Title,
            $"{(isVideo ? "Video" : "Image")} '{createdContent.Title}' created for {type} by {user.Nickname ?? user.UserName}",
            user.Id,
            user.UserName
        );

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

        var user = await _userManager.FindByIdAsync(userId);

        content.Update(title, description, sortOrder);
        await _naipeContentRepository.UpdateAsync(content);

        // Create audit log
        await CreateAuditLogAsync(
            content.IsVideo ? "Video Modified" : "Image Modified",
            content.Id,
            content.Title,
            $"{(content.IsVideo ? "Video" : "Image")} '{content.Title}' for {content.InstrumentType} modified by {user?.Nickname ?? user?.UserName ?? "Unknown"}",
            user?.Id,
            user?.UserName
        );
    }

    public async Task DeleteContentAsync(int id, string userId, bool isAdmin)
    {
        var content = await _naipeContentRepository.GetByIdOrThrowAsync(id);

        // Only owner or admin can delete content
        var isOwner = content.CreatedByUserId == userId;

        if (!isOwner && !isAdmin)
            throw new UnauthorizedAccessException("Only the content owner or administrators can delete naipe content.");

        var user = await _userManager.FindByIdAsync(userId);
        var contentType = content.IsVideo ? "Video" : "Image";
        var contentTitle = content.Title;
        var instrumentType = content.InstrumentType;
        var mediaUrl = content.Url;

        // Delete media from Cloudflare R2
        await _naipeMediaStorageService.DeleteMediaAsync(mediaUrl);

        await _naipeContentRepository.DeleteAsync(content);

        // Create audit log
        await CreateAuditLogAsync(
            content.IsVideo ? "Video Deleted" : "Image Deleted",
            id,
            contentTitle,
            $"{contentType} '{contentTitle}' for {instrumentType} deleted by {user?.Nickname ?? user?.UserName ?? "Unknown"}",
            user?.Id,
            user?.UserName,
            isCritical: true
        );
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

    public async Task<List<NaipeCommentDto>> GetCommentsAsync(int contentId, string? currentUserId)
    {
        var comments = await _naipeCommentRepository.QueryAsync(q => q
            .Include(c => c.Author)
            .Where(c => c.NaipeContentId == contentId && c.DeletedAt == null)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync());

        // Check if current user is admin
        bool isAdmin = await IsUserAdminAsync(currentUserId);

        return comments.Select(c => new NaipeCommentDto
        {
            Id = c.Id,
            NaipeContentId = c.NaipeContentId,
            AuthorId = c.AuthorId,
            AuthorName = c.Author.Nickname ?? c.Author.UserName ?? "Unknown",
            AuthorAvatarUrl = c.Author.ProfilePictureSrc,
            Text = c.Text,
            CreatedAt = c.CreatedAt,
            CanDelete = !string.IsNullOrEmpty(currentUserId) &&
                       (c.AuthorId == currentUserId || isAdmin)
        }).ToList();
    }

    public async Task<NaipeCommentDto> AddCommentAsync(int contentId, string authorId, string text)
    {
        _ = await _naipeContentRepository.GetByIdOrThrowAsync(contentId);

        var comment = NaipeComment.Create(contentId, authorId, text);
        var createdComment = await _naipeCommentRepository.AddAsync(comment);

        // Load author information
        var author = await _userManager.FindByIdAsync(authorId);
        if (author == null)
            throw new EntityNotFoundException(nameof(ApplicationUser), authorId);

        return new NaipeCommentDto
        {
            Id = createdComment.Id,
            NaipeContentId = createdComment.NaipeContentId,
            AuthorId = createdComment.AuthorId,
            AuthorName = author.Nickname ?? author.UserName ?? "Unknown",
            AuthorAvatarUrl = author.ProfilePictureSrc,
            Text = createdComment.Text,
            CreatedAt = createdComment.CreatedAt,
            CanDelete = true // Author can always delete their own comment
        };
    }

    public async Task DeleteCommentAsync(int commentId, string userId, bool isAdmin)
    {
        var comment = await _naipeCommentRepository.GetByIdOrThrowAsync(commentId);

        if (!CanDeleteComment(comment, userId, isAdmin))
            throw new UnauthorizedAccessException("You do not have permission to delete this comment");

        comment.SoftDelete();
        await _naipeCommentRepository.UpdateAsync(comment);
    }

    // Helper methods
    private async Task<bool> IsUserAdminAsync(string? userId)
    {
        if (string.IsNullOrEmpty(userId))
            return false;

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return false;

        var roles = await _userManager.GetRolesAsync(user);
        return roles.Contains("Admin") || roles.Contains("Owner");
    }
    private bool CanDeleteComment(NaipeComment comment, string userId, bool isAdmin)
    {
        // Admin/Owner can delete any comment
        if (isAdmin)
            return true;

        // Author can delete their own comment
        return comment.AuthorId == userId;
    }

    private NaipeContentDto MapToDto(NaipeContent content)
    {
        return new NaipeContentDto
        {
            Id = content.Id,
            InstrumentType = content.InstrumentType,
            Title = content.Title,
            Description = content.Description,
            Url = content.Url,
            MimeType = content.MimeType,
            IsVideo = content.IsVideo,
            SortOrder = content.SortOrder,
            CreatedByUserId = content.CreatedByUserId,
            CreatedByUserName = content.CreatedByUser?.Nickname ?? content.CreatedByUser?.UserName ?? "Unknown",
            CreatedAt = content.CreatedAt,
            PlayCount = content.PlayCounts?.Count ?? 0,
            CommentCount = content.Comments?.Count(c => c.DeletedAt == null) ?? 0
        };
    }

    private async Task CreateAuditLogAsync(string action, int entityId, string entityDisplayName, string changes, string? userId = null, string? userName = null, bool isCritical = false)
    {
        try
        {
            var resolvedUserId = userId;
            var resolvedUserName = userName;

            if (string.IsNullOrWhiteSpace(resolvedUserId))
            {
                resolvedUserId = _auditContext.UserId;
            }

            if (string.IsNullOrWhiteSpace(resolvedUserName))
            {
                resolvedUserName = _auditContext.UserName;
            }

            if (string.IsNullOrWhiteSpace(resolvedUserId) || string.IsNullOrWhiteSpace(resolvedUserName))
            {
                var httpUser = _httpContextAccessor.HttpContext?.User;
                resolvedUserId ??= httpUser?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                resolvedUserName ??= httpUser?.Identity?.Name ?? httpUser?.FindFirst(ClaimTypes.Name)?.Value;
            }

            using var auditCtx = _contextFactory.CreateDbContext();
            auditCtx.AuditLogs.Add(new AuditLog
            {
                EntityType = "NaipeContent",
                EntityId = entityId,
                Action = action,
                UserId = resolvedUserId,
                UserName = resolvedUserName,
                Timestamp = DateTime.UtcNow,
                Changes = changes,
                EntityDisplayName = entityDisplayName,
                IsCriticalAction = isCritical
            });
            await auditCtx.SaveChangesAsync();
        }
        catch
        {
            // Silently fail - audit logging should not break the application
        }
    }

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
            var instrumentTypeName = StatusHelper.GetInstrumentDisplay(instrumentType);
            var baseUrl = GetBaseUrl();

            var notification = _pushNotificationFactory.CreateNaipeContentNotification(
                contentTitle,
                instrumentTypeName,
                isVideo,
                baseUrl
            );

            await _pushNotificationService.BroadcastAsync(notification);
        }
        catch
        {
            // Silently fail - push notifications should not break content creation
        }
    }

    /// <summary>
    /// Gets the base URL from the current HTTP context.
    /// </summary>
    private string GetBaseUrl()
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        if (request != null)
        {
            return $"{request.Scheme}://{request.Host}";
        }
        return "https://rtub.pt"; // Fallback
    }
}
