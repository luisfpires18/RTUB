using Microsoft.AspNetCore.Identity;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using Microsoft.Extensions.Logging;


namespace RTUB.Application.Services;

/// <summary>
/// User profile service implementation
/// Handles user profile operations and business logic
/// </summary>
public class UserProfileService : IUserProfileService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly IImageService _imageService;
    private readonly IProfileStorageService _profileStorageService;
    private readonly ILogger<UserProfileService> _logger;

    public UserProfileService(
        UserManager<ApplicationUser> userManager, 
        ApplicationDbContext context, 
        IImageService imageService,
        IProfileStorageService profileStorageService,
        ILogger<UserProfileService> logger)
    {
        _userManager = userManager;
        _context = context;
        _imageService = imageService;
        _profileStorageService = profileStorageService;
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
        return await Task.FromResult(_userManager.Users.ToList());
    }

    public async Task UpdateProfilePictureAsync(string userId, byte[] imageData, string contentType)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            throw new InvalidOperationException($"User with ID {userId} not found");

        // STRATEGY: Delete-Before-Upload
        // Step 1: Delete old S3 image BEFORE uploading new one to avoid storing trash
        if (!string.IsNullOrEmpty(user.ImageUrl))
        {
            _logger.LogInformation("Deleting old profile picture from S3 for user {UserId}: {Filename}", 
                userId, user.ImageUrl);
            await _profileStorageService.DeleteImageAsync(user.ImageUrl);
        }
        
        // Step 2: Upload new image to S3 storage
        var username = user.UserName ?? user.Email ?? userId;
        var s3Filename = await _profileStorageService.UploadImageAsync(imageData, username, contentType);
        
        // Step 3: Update user with S3 filename
        user.ImageUrl = s3Filename;
        
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            throw new InvalidOperationException("Failed to update profile picture");
        
        // Invalidate the cached profile image so the new image is served immediately
        _imageService.InvalidateProfileImageCache(userId);
        
        _logger.LogInformation("Successfully uploaded profile picture to S3 for user {UserId}, filename: {Filename}", 
            userId, s3Filename);
    }

    public async Task UpdateUserInfoAsync(string userId, string firstName, string lastName, string? nickname, DateTime? dateOfBirth, string? phoneContact)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            throw new InvalidOperationException($"User with ID {userId} not found");

        user.FirstName = firstName;
        user.LastName = lastName;
        user.Nickname = nickname;
        user.DateOfBirth = dateOfBirth;
        user.PhoneContact = phoneContact;

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
            throw new InvalidOperationException($"User with ID {userId} not found");

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
            throw new InvalidOperationException($"User with ID {userId} not found");

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
            throw new InvalidOperationException($"User with ID {userId} not found");

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
}
