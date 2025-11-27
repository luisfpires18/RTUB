using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;


namespace RTUB.Application.Services;

/// <summary>
/// User profile service implementation
/// Handles user profile operations and business logic
/// Uses UserManager for Identity operations (correct pattern for ASP.NET Core Identity)
/// </summary>
public class UserProfileService : IUserProfileService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IImageStorageService _imageStorageService;
    private readonly ILeaderboardCommentRepository _leaderboardCommentRepository;
    private readonly ICommentRepository _commentRepository;
    private readonly IPostRepository _postRepository;
    private readonly IMeetingRepository _meetingRepository;
    private readonly IMeetingRequestRepository _meetingRequestRepository;
    private readonly ILogger<UserProfileService> _logger;

    public UserProfileService(
        UserManager<ApplicationUser> userManager,
        IImageStorageService imageStorageService,
        ILeaderboardCommentRepository leaderboardCommentRepository,
        ICommentRepository commentRepository,
        IPostRepository postRepository,
        IMeetingRepository meetingRepository,
        IMeetingRequestRepository meetingRequestRepository,
        ILogger<UserProfileService> logger)
    {
        _userManager = userManager;
        _imageStorageService = imageStorageService;
        _leaderboardCommentRepository = leaderboardCommentRepository;
        _commentRepository = commentRepository;
        _postRepository = postRepository;
        _meetingRepository = meetingRepository;
        _meetingRequestRepository = meetingRequestRepository;
        _logger = logger;
    }

    public async Task<ApplicationUser?> GetUserByIdAsync(string userId)
    {
        return await _userManager.FindByIdAsync(userId);
    }

    public async Task<ApplicationUser?> GetUserByUsernameAsync(string username)
    {
        return await _userManager.FindByNameAsync(username);
    }

    public async Task<ApplicationUser?> GetUserByEmailAsync(string email)
    {
        return await _userManager.FindByEmailAsync(email);
    }

    public async Task<IEnumerable<ApplicationUser>> GetAllUsersAsync()
    {
        // Note: Using ToListAsync when the underlying provider supports it (EF Core),
        // with fallback to synchronous ToList() for mocked UserManager in tests
        try
        {
            return await _userManager.Users.ToListAsync();
        }
        catch (InvalidOperationException)
        {
            // Fallback for test scenarios where UserManager.Users may not support async
            return _userManager.Users.ToList();
        }
    }

    public async Task UpdateProfilePictureAsync(string userId, Stream imageStream, string fileName, string contentType)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            throw new EntityNotFoundException(nameof(ApplicationUser), userId);

        // Delete old image if it exists
        if (!string.IsNullOrEmpty(user.ImageUrl))
        {
            await _imageStorageService.DeleteImageAsync(user.ImageUrl);
        }

        // Use username for the profile folder (avoids special characters)
        var profileIdentifier = user.UserName ?? userId;

        // Upload new image to Cloudflare R2
        var imageUrl = await _imageStorageService.UploadImageAsync(imageStream, fileName, contentType, "profile", profileIdentifier);
        user.ImageUrl = imageUrl;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            throw new InvalidOperationException("Failed to update profile picture");
    }

    public async Task UpdateUserInfoAsync(string userId, string firstName, string lastName, string? nickname, DateTime? dateOfBirth, string? phoneContact)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            throw new EntityNotFoundException(nameof(ApplicationUser), userId);

        user.FirstName = firstName;
        user.LastName = lastName;
        user.Nickname = nickname;
        user.DateOfBirth = dateOfBirth;
        user.PhoneNumber = phoneContact;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            throw new InvalidOperationException("Failed to update user information");
    }

    public async Task<bool> IsUserActiveAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        return user != null;
    }

    // User Role Management methods

    /// <summary>
    /// Gets all roles assigned to a user
    /// </summary>
    public async Task<List<string>> GetUserRolesAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            throw new EntityNotFoundException(nameof(ApplicationUser), userId);

        var roles = await _userManager.GetRolesAsync(user);
        return roles.ToList();
    }

    /// <summary>
    /// Adds a user to a specific role
    /// </summary>
    public async Task AddUserToRoleAsync(string userId, string roleName)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            throw new EntityNotFoundException(nameof(ApplicationUser), userId);

        if (string.IsNullOrWhiteSpace(roleName))
            throw new ArgumentException("Role name cannot be empty", nameof(roleName));

        var result = await _userManager.AddToRoleAsync(user, roleName);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to add user to role '{roleName}': {errors}");
        }
    }

    /// <summary>
    /// Removes a user from a specific role
    /// </summary>
    public async Task RemoveUserFromRoleAsync(string userId, string roleName)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            throw new EntityNotFoundException(nameof(ApplicationUser), userId);

        if (string.IsNullOrWhiteSpace(roleName))
            throw new ArgumentException("Role name cannot be empty", nameof(roleName));

        var result = await _userManager.RemoveFromRoleAsync(user, roleName);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to remove user from role '{roleName}': {errors}");
        }
    }

    /// <summary>
    /// Checks if a user is in a specific role
    /// </summary>
    public async Task<bool> IsUserInRoleAsync(string userId, string roleName)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return false;

        if (string.IsNullOrWhiteSpace(roleName))
            return false;

        return await _userManager.IsInRoleAsync(user, roleName);
    }

    /// <summary>
    /// Deletes a member and all related entities that have FK constraints preventing direct deletion
    /// Uses repository pattern and handles cleanup in proper order to avoid FK constraint violations
    /// </summary>
    public async Task<bool> DeleteMemberWithRelatedDataAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("Cannot delete member: User {UserId} not found", userId);
            return false;
        }

        try
        {
            // Delete related entities with Restrict/NoAction delete behaviors
            // Order matters: delete child entities first to avoid FK constraint errors

            // 1. Delete LeaderboardCommentLikes by UserId (likes on OTHER users' comments)
            // Load all comments that have likes from this user in a single query
            var commentsWithUserLikes = await _leaderboardCommentRepository.Query()
                .Include(c => c.Likes)
                .Where(c => c.Likes.Any(l => l.UserId == userId))
                .ToListAsync();

            foreach (var comment in commentsWithUserLikes)
            {
                var likeToRemove = comment.Likes.FirstOrDefault(l => l.UserId == userId);
                if (likeToRemove != null)
                {
                    comment.Likes.Remove(likeToRemove);
                }
            }

            // 2. Delete LeaderboardComments where AuthorId = userId OR TargetUserId = userId
            var leaderboardComments = await _leaderboardCommentRepository.Query()
                .Include(c => c.Likes)
                .Where(c => c.AuthorId == userId || c.TargetUserId == userId)
                .ToListAsync();

            foreach (var comment in leaderboardComments)
            {
                await _leaderboardCommentRepository.DeleteAsync(comment);
            }

            // 3. Delete Comments where AuthorId = userId
            var comments = await _commentRepository.Query()
                .Where(c => c.AuthorId == userId)
                .ToListAsync();

            foreach (var comment in comments)
            {
                await _commentRepository.DeleteAsync(comment);
            }

            // 4. Delete Posts where AuthorId = userId
            var posts = await _postRepository.Query()
                .Where(p => p.AuthorId == userId)
                .ToListAsync();

            foreach (var post in posts)
            {
                await _postRepository.DeleteAsync(post);
            }

            // 5. Set Meeting.OrganizerUserId to null where OrganizerUserId = userId
            var meetings = await _meetingRepository.Query()
                .Where(m => m.OrganizerUserId == userId)
                .ToListAsync();

            foreach (var meeting in meetings)
            {
                meeting.OrganizerUserId = null;
                await _meetingRepository.UpdateAsync(meeting);
            }

            // 6. Delete MeetingRequests where AuthorUserId = userId
            var meetingRequests = await _meetingRequestRepository.Query()
                .Where(mr => mr.AuthorUserId == userId)
                .ToListAsync();

            foreach (var meetingRequest in meetingRequests)
            {
                await _meetingRequestRepository.DeleteAsync(meetingRequest);
            }

            // 7. Delete the user using UserManager
            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogError("Failed to delete user {UserId}: {Errors}", userId, errors);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting member {UserId}", userId);
            return false;
        }
    }
}
