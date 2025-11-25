using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing leaderboard comments and likes using Repository pattern
/// Now depends on ILeaderboardCommentRepository abstraction instead of concrete DbContext
/// </summary>
public class LeaderboardCommentService : ILeaderboardCommentService
{
    private readonly ILeaderboardCommentRepository _leaderboardCommentRepository;
    private readonly UserManager<ApplicationUser> _userManager;

    public LeaderboardCommentService(
        ILeaderboardCommentRepository leaderboardCommentRepository,
        UserManager<ApplicationUser> userManager)
    {
        _leaderboardCommentRepository = leaderboardCommentRepository;
        _userManager = userManager;
    }

    /// <summary>
    /// Get all comments for a specific user (excluding deleted ones)
    /// </summary>
    public async Task<List<LeaderboardCommentDto>> GetCommentsForUserAsync(string targetUserId, string? currentUserId)
    {
        var comments = await _leaderboardCommentRepository.Query()
            .AsNoTracking()
            .Include(c => c.Author)
            .Include(c => c.Likes)
                .ThenInclude(l => l.User)
            .Where(c => c.TargetUserId == targetUserId && c.DeletedAt == null)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        // Check if current user is admin
        bool isAdmin = false;
        if (!string.IsNullOrEmpty(currentUserId))
        {
            var currentUser = await _userManager.FindByIdAsync(currentUserId);
            if (currentUser != null)
            {
                var roles = await _userManager.GetRolesAsync(currentUser);
                isAdmin = roles.Contains("Admin") || roles.Contains("Owner");
            }
        }

        return comments.Select(c => new LeaderboardCommentDto
        {
            Id = c.Id,
            TargetUserId = c.TargetUserId,
            AuthorId = c.AuthorId,
            AuthorName = c.Author.Nickname ?? c.Author.UserName ?? "Unknown",
            AuthorAvatarUrl = c.Author.ProfilePictureSrc,
            Text = c.Text,
            CreatedAt = c.CreatedAt,
            LikesCount = c.Likes.Count,
            IsLikedByCurrentUser = !string.IsNullOrEmpty(currentUserId) && 
                                   c.Likes.Any(l => l.UserId == currentUserId),
            CanDelete = !string.IsNullOrEmpty(currentUserId) && 
                       (c.AuthorId == currentUserId || isAdmin),
            LikedByNames = c.Likes
                .Where(l => l.User != null)
                .Select(l => l.User.Nickname ?? l.User.UserName ?? "Unknown")
                .ToList()
        }).ToList();
    }

    /// <summary>
    /// Add a new comment to a user's leaderboard profile
    /// </summary>
    public async Task<LeaderboardCommentDto> AddCommentAsync(string targetUserId, string authorId, string text)
    {
        var comment = LeaderboardComment.Create(targetUserId, authorId, text);
        
        var createdComment = await _leaderboardCommentRepository.AddAsync(comment);

        // Load author information
        var author = await _userManager.FindByIdAsync(authorId);
        if (author == null)
            throw new EntityNotFoundException(nameof(ApplicationUser), authorId);

        var roles = await _userManager.GetRolesAsync(author);
        var isAdmin = roles.Contains("Admin") || roles.Contains("Owner");

        return new LeaderboardCommentDto
        {
            Id = createdComment.Id,
            TargetUserId = createdComment.TargetUserId,
            AuthorId = createdComment.AuthorId,
            AuthorName = author.Nickname ?? author.UserName ?? "Unknown",
            AuthorAvatarUrl = author.ProfilePictureSrc,
            Text = createdComment.Text,
            CreatedAt = createdComment.CreatedAt,
            LikesCount = 0,
            IsLikedByCurrentUser = false,
            CanDelete = true // Author can always delete their own comment
        };
    }

    /// <summary>
    /// Delete a comment (soft delete)
    /// </summary>
    public async Task DeleteCommentAsync(int commentId, string userId, bool isAdmin)
    {
        var comment = await _leaderboardCommentRepository.Query()
            .Include(c => c.Likes)
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
        
        // Note: Likes will be handled by the repository through cascading delete
        await _leaderboardCommentRepository.UpdateAsync(comment);
    }

    /// <summary>
    /// Toggle a like on a comment (add if doesn't exist, remove if exists)
    /// </summary>
    public async Task<bool> ToggleLikeAsync(int commentId, string userId)
    {
        // Need to use Query() to access LeaderboardCommentLikes through the repository pattern
        // Since we don't have a dedicated repository for likes, we'll use the comment repository's query
        var existingLike = await _leaderboardCommentRepository.Query()
            .Where(c => c.Id == commentId)
            .SelectMany(c => c.Likes)
            .FirstOrDefaultAsync(l => l.UserId == userId);

        if (existingLike != null)
        {
            // Unlike - we need to get the comment and remove the like
            var comment = await _leaderboardCommentRepository.Query()
                .Include(c => c.Likes)
                .FirstOrDefaultAsync(c => c.Id == commentId);
            
            if (comment != null)
            {
                comment.Likes.Remove(existingLike);
                await _leaderboardCommentRepository.UpdateAsync(comment);
            }
            return false; // Unliked
        }
        else
        {
            // Like - add a new like
            var comment = await _leaderboardCommentRepository.Query()
                .Include(c => c.Likes)
                .FirstOrDefaultAsync(c => c.Id == commentId);
            
            if (comment != null)
            {
                var like = LeaderboardCommentLike.Create(commentId, userId);
                comment.Likes.Add(like);
                await _leaderboardCommentRepository.UpdateAsync(comment);
            }
            return true; // Liked
        }
    }

    /// <summary>
    /// Check if a user can delete a specific comment
    /// </summary>
    public bool CanDeleteComment(LeaderboardComment comment, string userId, bool isAdmin)
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
