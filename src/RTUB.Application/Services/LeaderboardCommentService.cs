using Microsoft.AspNetCore.Http;
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
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IPushNotificationFactory _pushNotificationFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public LeaderboardCommentService(
        ILeaderboardCommentRepository leaderboardCommentRepository,
        UserManager<ApplicationUser> userManager,
        IPushNotificationService pushNotificationService,
        IPushNotificationFactory pushNotificationFactory,
        IHttpContextAccessor httpContextAccessor)
    {
        _leaderboardCommentRepository = leaderboardCommentRepository;
        _userManager = userManager;
        _pushNotificationService = pushNotificationService;
        _pushNotificationFactory = pushNotificationFactory;
        _httpContextAccessor = httpContextAccessor;
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

        // Send notification to target user (but not if commenting on own profile)
        if (authorId != targetUserId)
        {
            await SendCommentNotificationAsync(author, targetUserId);
        }

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
    /// Optimized to use single query to check existing like and load comment together
    /// </summary>
    public async Task<bool> ToggleLikeAsync(int commentId, string userId)
    {
        // Use the repository's single-context method so Likes collection
        // changes are properly tracked and persisted (base UpdateAsync only
        // copies scalar properties via SetValues, losing navigation changes).
        var result = await _leaderboardCommentRepository.ToggleLikeAsync(commentId, userId);

        if (result == null)
        {
            return false; // Comment not found
        }

        // Send notification if liked (not unliked) and not liking own comment
        if (result == true)
        {
            // Need to get the comment author to check self-like
            var comment = await _leaderboardCommentRepository.GetByIdWithDetailsAsync(commentId);
            if (comment != null && userId != comment.AuthorId)
            {
                await SendLikeNotificationAsync(userId, comment.AuthorId);
            }
        }

        return result.Value;
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

    private async Task SendCommentNotificationAsync(ApplicationUser author, string targetUserId)
    {
        try
        {
            var targetUser = await _userManager.FindByIdAsync(targetUserId);
            if (targetUser == null)
                return;

            var authorName = author.Nickname ?? author.UserName ?? "Unknown";
            var targetUserName = targetUser.Nickname ?? targetUser.UserName ?? "Unknown";
            var baseUrl = GetBaseUrl();
            var notification = _pushNotificationFactory.CreateLeaderboardCommentNotification(authorName, targetUserName, baseUrl);

            await _pushNotificationService.SendToUserAsync(targetUserId, notification);
        }
        catch
        {
            // Log error but don't fail the operation
            // Notification is secondary to the main operation
        }
    }

    private async Task SendLikeNotificationAsync(string likerUserId, string commentAuthorId)
    {
        try
        {
            var liker = await _userManager.FindByIdAsync(likerUserId);
            if (liker == null)
                return;

            var likerName = liker.Nickname ?? liker.UserName ?? "Unknown";
            var baseUrl = GetBaseUrl();
            var notification = _pushNotificationFactory.CreateLeaderboardCommentLikeNotification(likerName, baseUrl);

            await _pushNotificationService.SendToUserAsync(commentAuthorId, notification);
        }
        catch
        {
            // Log error but don't fail the operation
            // Notification is secondary to the main operation
        }
    }

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
