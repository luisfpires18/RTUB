using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
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
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly IImageStorageService _imageStorageService;
    private readonly ILeaderboardCommentRepository _leaderboardCommentRepository;
    private readonly ICommentRepository _commentRepository;
    private readonly IPostRepository _postRepository;
    private readonly IMeetingRepository _meetingRepository;
    private readonly IMeetingRequestRepository _meetingRequestRepository;
    private readonly ILogger<UserProfileService> _logger;

    public UserProfileService(
        UserManager<ApplicationUser> userManager,
        IDbContextFactory<ApplicationDbContext> contextFactory,
        IImageStorageService imageStorageService,
        ILeaderboardCommentRepository leaderboardCommentRepository,
        ICommentRepository commentRepository,
        IPostRepository postRepository,
        IMeetingRepository meetingRepository,
        IMeetingRequestRepository meetingRequestRepository,
        ILogger<UserProfileService> logger)
    {
        _userManager = userManager;
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
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
        // Using AsNoTracking() for read-only operation
        try
        {
            return await _userManager.Users
                .AsNoTracking()
                .ToListAsync();
        }
        catch (InvalidOperationException)
        {
            // Fallback for test scenarios where UserManager.Users may not support async
            return _userManager.Users
                .AsNoTracking()
                .ToList();
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
    /// Deletes a member and all related entities that have FK constraints preventing direct deletion.
    /// Entities with Cascade/SetNull delete behaviors are handled automatically by the database.
    /// Entities with Restrict/NoAction/ClientSetNull must be manually cleaned up before user deletion.
    /// Uses a combination of repository pattern and direct DbContext access for comprehensive cleanup.
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
            // ===================================================================
            // Phase 1: Delete child entities of parents we'll delete later
            // (prevents FK violations in cascade chains)
            // ===================================================================

            var ctx = _contextFactory.CreateDbContext();

            // Delete GalleryMediaPersonTags for media uploaded by this user (before GalleryMedia)
            var uploadedMediaIds = await ctx.GalleryMedia
                .Where(gm => gm.UploaderId == userId)
                .Select(gm => gm.Id)
                .ToListAsync();
            if (uploadedMediaIds.Count > 0)
            {
                await ctx.GalleryMediaPersonTags
                    .Where(t => uploadedMediaIds.Contains(t.GalleryMediaId))
                    .ExecuteDeleteAsync();
            }

            // Delete NaipeComments and NaipePlayCounts for NaipeContents created by this user
            var userNaipeContentIds = await ctx.NaipeContents
                .Where(nc => nc.CreatedByUserId == userId)
                .Select(nc => nc.Id)
                .ToListAsync();
            if (userNaipeContentIds.Count > 0)
            {
                await ctx.NaipeComments
                    .Where(nc => userNaipeContentIds.Contains(nc.NaipeContentId))
                    .ExecuteDeleteAsync();
                await ctx.NaipePlayCounts
                    .Where(pc => userNaipeContentIds.Contains(pc.NaipeContentId))
                    .ExecuteDeleteAsync();
            }

            // Delete QuestionReplies for Questions authored by or assigned to this user
            var userQuestionIds = await ctx.Questions
                .Where(q => q.AuthorId == userId || q.AssignedMemberId == userId)
                .Select(q => q.Id)
                .ToListAsync();
            if (userQuestionIds.Count > 0)
            {
                await ctx.QuestionReplies
                    .Where(qr => userQuestionIds.Contains(qr.QuestionId))
                    .ExecuteDeleteAsync();
            }

            // Delete MeetingAta child entities for atas where PresidentUserId = userId
            var userPresidedAtaIds = await ctx.MeetingAtas
                .Where(ma => ma.PresidentUserId == userId)
                .Select(ma => ma.Id)
                .ToListAsync();
            if (userPresidedAtaIds.Count > 0)
            {
                await ctx.MeetingAtaAgendaPoints
                    .Where(ap => userPresidedAtaIds.Contains(ap.MeetingAtaId))
                    .ExecuteDeleteAsync();
                await ctx.MeetingAtaAttachments
                    .Where(a => userPresidedAtaIds.Contains(a.MeetingAtaId))
                    .ExecuteDeleteAsync();
                await ctx.MeetingAtaConfirmations
                    .Where(c => userPresidedAtaIds.Contains(c.MeetingAtaId))
                    .ExecuteDeleteAsync();
            }

            // ===================================================================
            // Phase 2: Delete/update direct Restrict FK references
            // ===================================================================

            // LeaderboardCommentLikes by UserId (likes on other users' comments)
            var commentsWithUserLikes = await _leaderboardCommentRepository.QueryAsync(q => q
                .Include(c => c.Likes)
                .Where(c => c.Likes.Any(l => l.UserId == userId))
                .ToListAsync());
            foreach (var comment in commentsWithUserLikes)
            {
                var likeToRemove = comment.Likes.FirstOrDefault(l => l.UserId == userId);
                if (likeToRemove != null)
                    comment.Likes.Remove(likeToRemove);
            }

            // LeaderboardComments where AuthorId or TargetUserId = userId
            var leaderboardComments = await _leaderboardCommentRepository.QueryAsync(q => q
                .Include(c => c.Likes)
                .Where(c => c.AuthorId == userId || c.TargetUserId == userId)
                .ToListAsync());
            foreach (var comment in leaderboardComments)
                await _leaderboardCommentRepository.DeleteAsync(comment);

            // Comments where AuthorId = userId
            var comments = await _commentRepository.QueryAsync(q => q
                .Where(c => c.AuthorId == userId)
                .ToListAsync());
            foreach (var comment in comments)
                await _commentRepository.DeleteAsync(comment);

            // Posts where AuthorId = userId
            var posts = await _postRepository.QueryAsync(q => q
                .Where(p => p.AuthorId == userId)
                .ToListAsync());
            foreach (var post in posts)
                await _postRepository.DeleteAsync(post);

            // BetComments by AuthorId
            await ctx.BetComments
                .Where(bc => bc.AuthorId == userId)
                .ExecuteDeleteAsync();

            // BetOptions - set nullable MemberAId/MemberBId to null
            await ctx.BetOptions
                .Where(bo => bo.MemberAId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(bo => bo.MemberAId, (string?)null));
            await ctx.BetOptions
                .Where(bo => bo.MemberBId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(bo => bo.MemberBId, (string?)null));

            // EventVideos by CreatedByUserId
            await ctx.EventVideos
                .Where(ev => ev.CreatedByUserId == userId)
                .ExecuteDeleteAsync();

            // GalleryMediaPersonTags where UserId = this user (tags of this user in other media)
            await ctx.GalleryMediaPersonTags
                .Where(t => t.UserId == userId)
                .ExecuteDeleteAsync();

            // GalleryMedia by UploaderId (person tags already deleted in Phase 1)
            await ctx.GalleryMedia
                .Where(gm => gm.UploaderId == userId)
                .ExecuteDeleteAsync();

            // NaipeComments by AuthorId (comments this user made on any content)
            await ctx.NaipeComments
                .Where(nc => nc.AuthorId == userId)
                .ExecuteDeleteAsync();

            // NaipeContents by CreatedByUserId (children deleted in Phase 1)
            await ctx.NaipeContents
                .Where(nc => nc.CreatedByUserId == userId)
                .ExecuteDeleteAsync();

            // QuestionReplies by AuthorId
            await ctx.QuestionReplies
                .Where(qr => qr.AuthorId == userId)
                .ExecuteDeleteAsync();

            // Questions by AuthorId or AssignedMemberId (children deleted in Phase 1)
            await ctx.Questions
                .Where(q => q.AuthorId == userId || q.AssignedMemberId == userId)
                .ExecuteDeleteAsync();

            // SongVideos by CreatedByUserId
            await ctx.SongVideos
                .Where(sv => sv.CreatedByUserId == userId)
                .ExecuteDeleteAsync();

            // ===================================================================
            // Phase 3: Handle NoAction FK relationships (nullify or delete)
            // ===================================================================

            // Meeting - set all user references to null
            var meetings = await _meetingRepository.QueryAsync(q => q
                .Where(m => m.OrganizerUserId == userId
                         || m.TunoRepresentativeUserId == userId
                         || m.DelegatedAtaWriterMemberId == userId)
                .ToListAsync());
            foreach (var meeting in meetings)
            {
                if (meeting.OrganizerUserId == userId) meeting.OrganizerUserId = null;
                if (meeting.TunoRepresentativeUserId == userId) meeting.TunoRepresentativeUserId = null;
                if (meeting.DelegatedAtaWriterMemberId == userId) meeting.DelegatedAtaWriterMemberId = null;
                await _meetingRepository.UpdateAsync(meeting);
            }

            // MeetingRequests by AuthorUserId
            var meetingRequests = await _meetingRequestRepository.QueryAsync(q => q
                .Where(mr => mr.AuthorUserId == userId)
                .ToListAsync());
            foreach (var meetingRequest in meetingRequests)
                await _meetingRequestRepository.DeleteAsync(meetingRequest);

            // MeetingAtas - delete where PresidentUserId = userId (required field, children deleted in Phase 1)
            await ctx.MeetingAtas
                .Where(ma => ma.PresidentUserId == userId)
                .ExecuteDeleteAsync();

            // MeetingAtas - set nullable secretary fields to null
            await ctx.MeetingAtas
                .Where(ma => ma.FirstSecretaryUserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(ma => ma.FirstSecretaryUserId, (string?)null));
            await ctx.MeetingAtas
                .Where(ma => ma.SecondSecretaryUserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(ma => ma.SecondSecretaryUserId, (string?)null));

            // ===================================================================
            // Phase 4: Handle ClientSetNull FK relationships
            // (EF Core only handles these for tracked entities, not at DB level)
            // ===================================================================

            // LogisticsCards - set AssignedToUserId to null
            await ctx.LogisticsCards
                .Where(lc => lc.AssignedToUserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(lc => lc.AssignedToUserId, (string?)null));

            // ===================================================================
            // Phase 5: Delete the user (Cascade/SetNull FKs handled by DB)
            // ===================================================================
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

    /// <summary>
    /// Gets a user's categories (MemberCategory) without change tracking.
    /// Useful for read-only checks like determining if a user is a Leitão.
    /// </summary>
    /// <param name="userId">The user ID to get categories for</param>
    /// <returns>The user's categories, or empty collection if user not found</returns>
    public async Task<IEnumerable<MemberCategory>> GetUserCategoriesAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return Enumerable.Empty<MemberCategory>();

        var ctx = _contextFactory.CreateDbContext();
        var user = await ctx.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.Categories)
            .FirstOrDefaultAsync();

        return user ?? Enumerable.Empty<MemberCategory>();
    }
}
