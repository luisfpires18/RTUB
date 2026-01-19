using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing bet comments using Repository pattern
/// </summary>
public class BetCommentService : IBetCommentService
{
    private readonly IBetCommentRepository _betCommentRepository;
    private readonly UserManager<ApplicationUser> _userManager;

    public BetCommentService(
        IBetCommentRepository betCommentRepository,
        UserManager<ApplicationUser> userManager)
    {
        _betCommentRepository = betCommentRepository;
        _userManager = userManager;
    }

    /// <summary>
    /// Get all comments for a specific bet (excluding deleted ones)
    /// </summary>
    public async Task<List<BetCommentDto>> GetCommentsForBetAsync(int betId, string? currentUserId, bool isAdmin = false)
    {
        var comments = await _betCommentRepository.Query()
            .AsNoTracking()
            .Include(c => c.Author)
            .Where(c => c.BetId == betId && c.DeletedAt == null)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        return comments.Select(c => new BetCommentDto
        {
            Id = c.Id,
            BetId = c.BetId,
            AuthorId = c.AuthorId,
            AuthorName = c.Author.Nickname ?? c.Author.UserName ?? "Unknown",
            AuthorAvatarUrl = c.Author.ProfilePictureSrc,
            Text = c.Text,
            MediaUrl = c.MediaUrl,
            MediaType = c.MediaType,
            CreatedAt = c.CreatedAt,
            CanDelete = !string.IsNullOrEmpty(currentUserId) &&
                       (c.AuthorId == currentUserId || isAdmin)
        }).ToList();
    }
    
    /// <summary>
    /// Get comment count for a specific bet (excluding deleted ones)
    /// </summary>
    public async Task<int> GetCommentCountForBetAsync(int betId)
    {
        return await _betCommentRepository.Query()
            .AsNoTracking()
            .Where(c => c.BetId == betId && c.DeletedAt == null)
            .CountAsync();
    }

    /// <summary>
    /// Add a new comment to a bet
    /// </summary>
    public async Task<BetCommentDto> AddCommentAsync(int betId, string authorId, string text, string? mediaUrl = null, string? mediaType = null)
    {
        var comment = BetComment.Create(betId, authorId, text, mediaUrl, mediaType);

        var createdComment = await _betCommentRepository.AddAsync(comment);

        // Load author information
        var author = await _userManager.FindByIdAsync(authorId);
        if (author == null)
            throw new EntityNotFoundException(nameof(ApplicationUser), authorId);

        return new BetCommentDto
        {
            Id = createdComment.Id,
            BetId = createdComment.BetId,
            AuthorId = createdComment.AuthorId,
            AuthorName = author.Nickname ?? author.UserName ?? "Unknown",
            AuthorAvatarUrl = author.ProfilePictureSrc,
            Text = createdComment.Text,
            MediaUrl = createdComment.MediaUrl,
            MediaType = createdComment.MediaType,
            CreatedAt = createdComment.CreatedAt,
            CanDelete = true // Author can always delete their own comment
        };
    }

    /// <summary>
    /// Delete a comment (soft delete)
    /// </summary>
    public async Task DeleteCommentAsync(int commentId, string userId, bool isAdmin)
    {
        var comment = await _betCommentRepository.Query()
            .FirstOrDefaultAsync(c => c.Id == commentId);

        if (comment == null)
        {
            throw new InvalidOperationException("Comment not found");
        }

        if (!CanDeleteComment(comment, userId, isAdmin))
        {
            throw new UnauthorizedAccessException("You do not have permission to delete this comment");
        }

        // Soft delete the comment
        comment.SoftDelete();

        await _betCommentRepository.UpdateAsync(comment);
    }

    /// <summary>
    /// Check if a user can delete a specific comment
    /// </summary>
    public bool CanDeleteComment(BetComment comment, string userId, bool isAdmin)
    {
        // Admin/Owner can delete any comment
        if (isAdmin)
        {
            return true;
        }

        // Users can delete their own comments
        return comment.AuthorId == userId;
    }
}
