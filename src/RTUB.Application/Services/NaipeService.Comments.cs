// Naipe: comments, delete permission, DTO mapping and audit log.
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Services;

public partial class NaipeService
{
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
}
