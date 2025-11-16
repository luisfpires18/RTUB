using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing leaderboard comments and likes
/// </summary>
public class LeaderboardCommentService : ILeaderboardCommentService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public LeaderboardCommentService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    /// <summary>
    /// Get all comments for a specific user (excluding deleted ones)
    /// </summary>
    public async Task<List<LeaderboardCommentDto>> GetCommentsForUserAsync(string targetUserId, string? currentUserId)
    {
        var comments = await _context.LeaderboardComments
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
        
        _context.LeaderboardComments.Add(comment);
        await _context.SaveChangesAsync();

        // Reload with author information
        await _context.Entry(comment)
            .Reference(c => c.Author)
            .LoadAsync();

        // Check if author is admin
        var author = await _userManager.FindByIdAsync(authorId);
        var roles = await _userManager.GetRolesAsync(author!);
        var isAdmin = roles.Contains("Admin") || roles.Contains("Owner");

        return new LeaderboardCommentDto
        {
            Id = comment.Id,
            TargetUserId = comment.TargetUserId,
            AuthorId = comment.AuthorId,
            AuthorName = comment.Author.Nickname ?? comment.Author.UserName ?? "Unknown",
            AuthorAvatarUrl = comment.Author.ProfilePictureSrc,
            Text = comment.Text,
            CreatedAt = comment.CreatedAt,
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
        var comment = await _context.LeaderboardComments
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
        
        // Remove all likes when comment is deleted
        _context.LeaderboardCommentLikes.RemoveRange(comment.Likes);
        
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Toggle a like on a comment (add if doesn't exist, remove if exists)
    /// </summary>
    public async Task<bool> ToggleLikeAsync(int commentId, string userId)
    {
        var existingLike = await _context.LeaderboardCommentLikes
            .FirstOrDefaultAsync(l => l.CommentId == commentId && l.UserId == userId);

        if (existingLike != null)
        {
            // Unlike - remove the like
            _context.LeaderboardCommentLikes.Remove(existingLike);
            await _context.SaveChangesAsync();
            return false; // Unliked
        }
        else
        {
            // Like - add a new like
            var like = LeaderboardCommentLike.Create(commentId, userId);
            _context.LeaderboardCommentLikes.Add(like);
            await _context.SaveChangesAsync();
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
