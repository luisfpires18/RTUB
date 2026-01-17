using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
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
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly AuditContext _auditContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public NaipeService(
        INaipeContentRepository naipeContentRepository,
        INaipeCommentRepository naipeCommentRepository,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context,
        AuditContext auditContext,
        IHttpContextAccessor httpContextAccessor)
    {
        _naipeContentRepository = naipeContentRepository;
        _naipeCommentRepository = naipeCommentRepository;
        _userManager = userManager;
        _context = context;
        _auditContext = auditContext;
        _httpContextAccessor = httpContextAccessor;
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

    public async Task<NaipeContentDto> CreateContentAsync(InstrumentType type, string title, string? description, string url, string mimeType, bool isVideo, decimal sortOrder, string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            throw new EntityNotFoundException(nameof(ApplicationUser), userId);

        var content = NaipeContent.Create(type, title, url, mimeType, isVideo, sortOrder, userId, description);
        var createdContent = await _naipeContentRepository.AddAsync(content);

        // Create audit log
        await CreateAuditLogAsync(
            isVideo ? "Video Created" : "Image Created",
            createdContent.Id,
            createdContent.Title,
            $"{(isVideo ? "Video" : "Image")} '{createdContent.Title}' created for {type} by {user.Nickname ?? user.UserName}"
        );

        return MapToDto(createdContent);
    }

    public async Task UpdateContentAsync(int id, string title, string? description, decimal sortOrder)
    {
        var content = await _naipeContentRepository.GetByIdAsync(id);
        if (content == null)
            throw new EntityNotFoundException(nameof(NaipeContent), id);

        var user = await _userManager.FindByIdAsync(_auditContext.UserId ?? string.Empty);

        content.Update(title, description, sortOrder);
        await _naipeContentRepository.UpdateAsync(content);

        // Create audit log
        await CreateAuditLogAsync(
            content.IsVideo ? "Video Modified" : "Image Modified",
            content.Id,
            content.Title,
            $"{(content.IsVideo ? "Video" : "Image")} '{content.Title}' for {content.InstrumentType} modified by {user?.Nickname ?? user?.UserName ?? "Unknown"}"
        );
    }

    public async Task DeleteContentAsync(int id)
    {
        var content = await _naipeContentRepository.GetByIdAsync(id);
        if (content == null)
            throw new EntityNotFoundException(nameof(NaipeContent), id);

        var user = await _userManager.FindByIdAsync(_auditContext.UserId ?? string.Empty);
        var contentType = content.IsVideo ? "Video" : "Image";
        var contentTitle = content.Title;
        var instrumentType = content.InstrumentType;

        await _naipeContentRepository.DeleteAsync(content);

        // Create audit log
        await CreateAuditLogAsync(
            content.IsVideo ? "Video Deleted" : "Image Deleted",
            id,
            contentTitle,
            $"{contentType} '{contentTitle}' for {instrumentType} deleted by {user?.Nickname ?? user?.UserName ?? "Unknown"}",
            isCritical: true
        );
    }

    public async Task IncrementPlayCountAsync(int contentId, string? userId)
    {
        var content = await _naipeContentRepository.GetByIdAsync(contentId);
        if (content == null)
            throw new EntityNotFoundException(nameof(NaipeContent), contentId);

        var playCount = new NaipePlayCount
        {
            NaipeContentId = contentId,
            UserId = userId,
            PlayedAt = DateTime.UtcNow
        };

        _context.NaipePlayCounts.Add(playCount);
        await _context.SaveChangesAsync();

        // Create audit log (only if user is authenticated to avoid spam)
        if (!string.IsNullOrEmpty(userId))
        {
            var user = await _userManager.FindByIdAsync(userId);
            await CreateAuditLogAsync(
                content.IsVideo ? "Video Played" : "Image Viewed",
                content.Id,
                content.Title,
                $"{(content.IsVideo ? "Video" : "Image")} '{content.Title}' for {content.InstrumentType} {(content.IsVideo ? "played" : "viewed")} by {user?.Nickname ?? user?.UserName ?? "Unknown"}"
            );
        }
    }

    public async Task<int> GetPlayCountAsync(int contentId)
    {
        return await _naipeContentRepository.GetPlayCountAsync(contentId);
    }

    public async Task<List<NaipeCommentDto>> GetCommentsAsync(int contentId, string? currentUserId)
    {
        var comments = await _naipeCommentRepository.Query()
            .AsNoTracking()
            .Include(c => c.Author)
            .Where(c => c.NaipeContentId == contentId && c.DeletedAt == null)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

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
        var content = await _naipeContentRepository.GetByIdAsync(contentId);
        if (content == null)
            throw new EntityNotFoundException(nameof(NaipeContent), contentId);

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
        var comment = await _naipeCommentRepository.GetByIdAsync(commentId);
        if (comment == null)
            throw new InvalidOperationException("Comment not found");

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
            CreatedByUserName = content.CreatedByUser?.Nickname ?? content.CreatedByUser?.UserName ?? "Unknown",
            CreatedAt = content.CreatedAt,
            PlayCount = content.PlayCounts?.Count ?? 0,
            CommentCount = content.Comments?.Count(c => c.DeletedAt == null) ?? 0
        };
    }

    private async Task CreateAuditLogAsync(string action, int entityId, string entityDisplayName, string changes, bool isCritical = false)
    {
        try
        {
            _context.AuditLogs.Add(new AuditLog
            {
                EntityType = "NaipeContent",
                EntityId = entityId,
                Action = action,
                UserId = _auditContext.UserId,
                UserName = _auditContext.UserName,
                Timestamp = DateTime.UtcNow,
                Changes = changes,
                EntityDisplayName = entityDisplayName,
                IsCriticalAction = isCritical
            });
            await _context.SaveChangesAsync();
        }
        catch
        {
            // Silently fail - audit logging should not break the application
        }
    }
}
