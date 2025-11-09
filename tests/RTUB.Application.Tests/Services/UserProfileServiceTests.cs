using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace RTUB.Application.Tests.Services;

public class UserProfileServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<IImageService> _mockImageService;
    private readonly Mock<IProfileStorageService> _mockProfileStorageService;
    private readonly Mock<ILogger<UserProfileService>> _mockLogger;
    private readonly UserProfileService _service;

    public UserProfileServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        
        // Mock UserManager
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        _mockUserManager = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null, null, null, null, null, null, null, null);
        
        _mockImageService = new Mock<IImageService>();
        _mockProfileStorageService = new Mock<IProfileStorageService>();
        _mockLogger = new Mock<ILogger<UserProfileService>>();
        
        _service = new UserProfileService(
            _mockUserManager.Object, 
            _context, 
            _mockImageService.Object,
            _mockProfileStorageService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task GetUserByIdAsync_WithValidId_ReturnsUser()
    {
        // Arrange
        var userId = "user-123";
        var user = new ApplicationUser { Id = userId, UserName = "testuser" };
        _mockUserManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync(user);

        // Act
        var result = await _service.GetUserByIdAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(userId);
        result.UserName.Should().Be("testuser");
    }

    [Fact]
    public async Task GetUserByIdAsync_WithInvalidId_ReturnsNull()
    {
        // Arrange
        _mockUserManager.Setup(x => x.FindByIdAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);

        // Act
        var result = await _service.GetUserByIdAsync("invalid-id");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUserByUsernameAsync_WithValidUsername_ReturnsUser()
    {
        // Arrange
        var username = "testuser";
        var user = new ApplicationUser { Id = "user-123", UserName = username };
        _mockUserManager.Setup(x => x.FindByNameAsync(username)).ReturnsAsync(user);

        // Act
        var result = await _service.GetUserByUsernameAsync(username);

        // Assert
        result.Should().NotBeNull();
        result!.UserName.Should().Be(username);
    }

    [Fact]
    public async Task GetUserByEmailAsync_WithValidEmail_ReturnsUser()
    {
        // Arrange
        var email = "test@example.com";
        var user = new ApplicationUser { Id = "user-123", Email = email };
        _mockUserManager.Setup(x => x.FindByEmailAsync(email)).ReturnsAsync(user);

        // Act
        var result = await _service.GetUserByEmailAsync(email);

        // Assert
        result.Should().NotBeNull();
        result!.Email.Should().Be(email);
    }

    [Fact]
    public async Task GetAllUsersAsync_ReturnsAllUsers()
    {
        // Arrange
        var users = new List<ApplicationUser>
        {
            new() { Id = "user-1", UserName = "user1" },
            new() { Id = "user-2", UserName = "user2" },
            new() { Id = "user-3", UserName = "user3" }
        }.AsQueryable();

        var mockDbSet = new Mock<DbSet<ApplicationUser>>();
        mockDbSet.As<IQueryable<ApplicationUser>>().Setup(m => m.Provider).Returns(users.Provider);
        mockDbSet.As<IQueryable<ApplicationUser>>().Setup(m => m.Expression).Returns(users.Expression);
        mockDbSet.As<IQueryable<ApplicationUser>>().Setup(m => m.ElementType).Returns(users.ElementType);
        mockDbSet.As<IQueryable<ApplicationUser>>().Setup(m => m.GetEnumerator()).Returns(users.GetEnumerator());

        _mockUserManager.Setup(x => x.Users).Returns(mockDbSet.Object);

        // Act
        var result = await _service.GetAllUsersAsync();

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task UpdateProfilePictureAsync_WithValidUser_UpdatesProfilePicture()
    {
        // Arrange
        var userId = "user-123";
        var user = new ApplicationUser { Id = userId, UserName = "testuser" };
        var imageData = new byte[] { 1, 2, 3, 4 };
        var contentType = "image/jpeg";
        var s3Filename = "testuser_20240101120000.webp";

        _mockUserManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);
        _mockProfileStorageService.Setup(x => x.UploadImageAsync(imageData, It.IsAny<string>(), contentType))
            .ReturnsAsync(s3Filename);

        // Act
        await _service.UpdateProfilePictureAsync(userId, imageData, contentType);

        // Assert
        user.ImageUrl.Should().Be(s3Filename);
        _mockUserManager.Verify(x => x.UpdateAsync(user), Times.Once);
        _mockImageService.Verify(x => x.InvalidateProfileImageCache(userId), Times.Once);
        _mockProfileStorageService.Verify(x => x.UploadImageAsync(imageData, It.IsAny<string>(), contentType), Times.Once);
    }

    [Fact]
    public async Task UpdateProfilePictureAsync_WithInvalidUser_ThrowsException()
    {
        // Arrange
        _mockUserManager.Setup(x => x.FindByIdAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);

        // Act
        var act = async () => await _service.UpdateProfilePictureAsync("invalid-id", new byte[0], "image/jpeg");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("User with ID invalid-id not found");
    }

    [Fact]
    public async Task UpdateProfilePictureAsync_WhenUpdateFails_ThrowsException()
    {
        // Arrange
        var userId = "user-123";
        var user = new ApplicationUser { Id = userId };
        _mockUserManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Update failed" }));

        // Act
        var act = async () => await _service.UpdateProfilePictureAsync(userId, new byte[0], "image/jpeg");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Failed to update profile picture");
    }

    [Fact]
    public async Task UpdateUserInfoAsync_WithValidUser_UpdatesUserInfo()
    {
        // Arrange
        var userId = "user-123";
        var user = new ApplicationUser { Id = userId };
        var firstName = "John";
        var lastName = "Doe";
        var nickname = "JD";
        var dateOfBirth = new DateTime(1990, 1, 1);
        var phoneContact = "+351 912345678";

        _mockUserManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        await _service.UpdateUserInfoAsync(userId, firstName, lastName, nickname, dateOfBirth, phoneContact);

        // Assert
        user.FirstName.Should().Be(firstName);
        user.LastName.Should().Be(lastName);
        user.Nickname.Should().Be(nickname);
        user.DateOfBirth.Should().Be(dateOfBirth);
        user.PhoneContact.Should().Be(phoneContact);
        _mockUserManager.Verify(x => x.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task UpdateUserInfoAsync_WithInvalidUser_ThrowsException()
    {
        // Arrange
        _mockUserManager.Setup(x => x.FindByIdAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);

        // Act
        var act = async () => await _service.UpdateUserInfoAsync("invalid-id", "John", "Doe", null, null, null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("User with ID invalid-id not found");
    }

    [Fact]
    public async Task UpdateUserInfoAsync_WhenUpdateFails_ThrowsException()
    {
        // Arrange
        var userId = "user-123";
        var user = new ApplicationUser { Id = userId };
        _mockUserManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Update failed" }));

        // Act
        var act = async () => await _service.UpdateUserInfoAsync(userId, "John", "Doe", null, null, null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Failed to update user information");
    }

    [Fact]
    public async Task IsUserActiveAsync_WithExistingUser_ReturnsTrue()
    {
        // Arrange
        var userId = "user-123";
        var user = new ApplicationUser { Id = userId };
        _mockUserManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync(user);

        // Act
        var result = await _service.IsUserActiveAsync(userId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsUserActiveAsync_WithNonExistingUser_ReturnsFalse()
    {
        // Arrange
        _mockUserManager.Setup(x => x.FindByIdAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);

        // Act
        var result = await _service.IsUserActiveAsync("invalid-id");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetUserRolesAsync_WithValidUser_ReturnsRoles()
    {
        // Arrange
        var userId = "user-123";
        var user = new ApplicationUser { Id = userId };
        var roles = new List<string> { "Admin", "Member" };

        _mockUserManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(roles);

        // Act
        var result = await _service.GetUserRolesAsync(userId);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain("Admin");
        result.Should().Contain("Member");
    }

    [Fact]
    public async Task GetUserRolesAsync_WithInvalidUser_ThrowsException()
    {
        // Arrange
        _mockUserManager.Setup(x => x.FindByIdAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);

        // Act
        var act = async () => await _service.GetUserRolesAsync("invalid-id");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("User with ID invalid-id not found");
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
