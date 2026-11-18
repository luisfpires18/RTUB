using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Services;

/// <summary>
/// User profile service implementation
/// Handles user profile operations and business logic
/// Uses UserManager for Identity operations (correct pattern for ASP.NET Core Identity)
/// </summary>
public partial class UserProfileService : IUserProfileService
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

    public async Task UpdateProfilePictureAsync(string userId, Stream imageStream, string fileName, string contentType)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            throw new EntityNotFoundException(nameof(ApplicationUser), userId);

        // Use username for the profile folder (avoids special characters)
        var profileIdentifier = user.UserName ?? userId;

        // Upload first: a failed upload must leave the member with the picture they had.
        var oldImageUrl = user.ImageUrl;
        var imageUrl = await _imageStorageService.UploadImageAsync(imageStream, fileName, contentType, "profile", profileIdentifier);
        user.ImageUrl = imageUrl;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            throw new InvalidOperationException("Failed to update profile picture");

        // The object name carries the time to the second: two uploads in one second share it, and it must stay.
        if (!string.IsNullOrEmpty(oldImageUrl) && oldImageUrl != imageUrl)
        {
            await _imageStorageService.DeleteImageAsync(oldImageUrl);
        }
    }

}
