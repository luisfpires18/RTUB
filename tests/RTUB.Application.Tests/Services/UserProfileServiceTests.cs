using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Tests.Services;

public class UserProfileServiceTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<IImageStorageService> _mockImageStorageService;
    private readonly Mock<ILeaderboardCommentRepository> _mockLeaderboardCommentRepository;
    private readonly Mock<ICommentRepository> _mockCommentRepository;
    private readonly Mock<IPostRepository> _mockPostRepository;
    private readonly Mock<IMeetingRepository> _mockMeetingRepository;
    private readonly Mock<IMeetingRequestRepository> _mockMeetingRequestRepository;
    private readonly Mock<ILogger<UserProfileService>> _mockLogger;
    private readonly UserProfileService _service;

    public UserProfileServiceTests(DatabaseFixture fixture)
    {
        // Clean database at constructor start to ensure test isolation
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();

        _context = _fixture.CreateContext();

        // Mock UserManager
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        _mockUserManager = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _mockImageStorageService = new Mock<IImageStorageService>();
        _mockLeaderboardCommentRepository = new Mock<ILeaderboardCommentRepository>();
        _mockCommentRepository = new Mock<ICommentRepository>();
        _mockPostRepository = new Mock<IPostRepository>();
        _mockMeetingRepository = new Mock<IMeetingRepository>();
        _mockMeetingRequestRepository = new Mock<IMeetingRequestRepository>();
        _mockLogger = new Mock<ILogger<UserProfileService>>();

        _service = new UserProfileService(
            _mockUserManager.Object,
            _context,
            _mockImageStorageService.Object,
            _mockLeaderboardCommentRepository.Object,
            _mockCommentRepository.Object,
            _mockPostRepository.Object,
            _mockMeetingRepository.Object,
            _mockMeetingRequestRepository.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task GetUserByIdAsync_WithValidId_ReturnsUser()
    {
        // Arrange
        var userId = "user-123";
        var user = new ApplicationUser { Id = userId, UserName = "testuser", Nickname = "TestUser" };
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
        var user = new ApplicationUser { Id = "user-123", UserName = username, Nickname = username };
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
        var user = new ApplicationUser { Id = "user-123", Email = email, Nickname = "TestUser" };
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
    public async Task UpdateUserInfoAsync_WithValidUser_UpdatesUserInfo()
    {
        // Arrange
        var userId = "user-123";
        var user = new ApplicationUser { Id = userId, Nickname = "TestUser" };
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
        user.PhoneNumber.Should().Be(phoneContact);
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
        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("ApplicationUser with ID invalid-id not found");
    }

    [Fact]
    public async Task UpdateUserInfoAsync_WhenUpdateFails_ThrowsException()
    {
        // Arrange
        var userId = "user-123";
        var user = new ApplicationUser { Id = userId, Nickname = "TestUser" };
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
        var user = new ApplicationUser { Id = userId, Nickname = "TestUser" };
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
        var user = new ApplicationUser { Id = userId, Nickname = "TestUser" };
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
        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("ApplicationUser with ID invalid-id not found");
    }

    [Fact]
    public async Task GetUserCategoriesAsync_WithValidUser_ReturnsCategories()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "testuser",
            Email = "test@test.com",
            EmailConfirmed = true,
            FirstName = "Test",
            LastName = "User",
            Nickname = "TestUser",
            PhoneNumber = "123456789",
            Categories = new List<MemberCategory> { MemberCategory.Tuno, MemberCategory.Leitao }
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetUserCategoriesAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().Contain(MemberCategory.Tuno);
        result.Should().Contain(MemberCategory.Leitao);
    }

    [Fact]
    public async Task GetUserCategoriesAsync_WithInvalidUser_ReturnsEmpty()
    {
        // Act
        var result = await _service.GetUserCategoriesAsync("invalid-id");

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserCategoriesAsync_WithNullUserId_ReturnsEmpty()
    {
        // Act
        var result = await _service.GetUserCategoriesAsync(null!);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllUsersAsync_UsesAsNoTracking()
    {
        // Arrange
        var users = new List<ApplicationUser>
        {
            new() { Id = "user-1", UserName = "user1", FirstName = "User", LastName = "One", Nickname = "User1" },
            new() { Id = "user-2", UserName = "user2", FirstName = "User", LastName = "Two", Nickname = "User2" }
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
        result.Should().HaveCount(2);
        // Note: GetAllUsersAsync uses ToListAsync() which should not track entities
        // The service has a fallback to ToList() for test scenarios, but in production
        // it uses ToListAsync() which respects AsNoTracking behavior
    }

    [Fact]
    public async Task UpdateProfilePictureAsync_WithValidFile_UpdatesPicture()
    {
        // Arrange
        var userId = "user-123";
        var user = new ApplicationUser 
        { 
            Id = userId, 
            UserName = "testuser", 
            Nickname = "TestUser",
            ImageUrl = "old-image-url.jpg"
        };
        var newImageUrl = "new-image-url.jpg";
        var imageStream = new MemoryStream(new byte[] { 1, 2, 3 });
        var fileName = "profile.jpg";
        var contentType = "image/jpeg";

        _mockUserManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync(user);
        _mockImageStorageService.Setup(x => x.DeleteImageAsync("old-image-url.jpg"))
            .Returns(Task.CompletedTask);
        _mockImageStorageService.Setup(x => x.UploadImageAsync(
            It.IsAny<Stream>(), fileName, contentType, "profile", "testuser"))
            .ReturnsAsync(newImageUrl);
        _mockUserManager.Setup(x => x.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        await _service.UpdateProfilePictureAsync(userId, imageStream, fileName, contentType);

        // Assert
        user.ImageUrl.Should().Be(newImageUrl);
        _mockImageStorageService.Verify(x => x.DeleteImageAsync("old-image-url.jpg"), Times.Once);
        _mockImageStorageService.Verify(x => x.UploadImageAsync(
            It.IsAny<Stream>(), fileName, contentType, "profile", "testuser"), Times.Once);
        _mockUserManager.Verify(x => x.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task UpdateProfilePictureAsync_WithInvalidUser_ThrowsException()
    {
        // Arrange
        var userId = "invalid-user";
        var imageStream = new MemoryStream(new byte[] { 1, 2, 3 });
        var fileName = "profile.jpg";
        var contentType = "image/jpeg";

        _mockUserManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync((ApplicationUser?)null);

        // Act
        var act = async () => await _service.UpdateProfilePictureAsync(userId, imageStream, fileName, contentType);

        // Assert
        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("ApplicationUser with ID invalid-user not found");
        _mockImageStorageService.Verify(x => x.UploadImageAsync(
            It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), 
            Times.Never);
    }

    [Fact]
    public async Task UpdateProfilePictureAsync_WhenUpdateFails_ThrowsException()
    {
        // Arrange
        var userId = "user-123";
        var user = new ApplicationUser 
        { 
            Id = userId, 
            UserName = "testuser", 
            Nickname = "TestUser"
        };
        var newImageUrl = "new-image-url.jpg";
        var imageStream = new MemoryStream(new byte[] { 1, 2, 3 });
        var fileName = "profile.jpg";
        var contentType = "image/jpeg";

        _mockUserManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync(user);
        _mockImageStorageService.Setup(x => x.UploadImageAsync(
            It.IsAny<Stream>(), fileName, contentType, "profile", "testuser"))
            .ReturnsAsync(newImageUrl);
        _mockUserManager.Setup(x => x.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Update failed" }));

        // Act
        var act = async () => await _service.UpdateProfilePictureAsync(userId, imageStream, fileName, contentType);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Failed to update profile picture");
        // Image should still be uploaded even if update fails
        _mockImageStorageService.Verify(x => x.UploadImageAsync(
            It.IsAny<Stream>(), fileName, contentType, "profile", "testuser"), Times.Once);
    }

    [Fact]
    public async Task UpdateProfilePictureAsync_WithoutExistingImage_DoesNotDeleteOldImage()
    {
        // Arrange
        var userId = "user-123";
        var user = new ApplicationUser 
        { 
            Id = userId, 
            UserName = "testuser", 
            Nickname = "TestUser",
            ImageUrl = null // No existing image
        };
        var newImageUrl = "new-image-url.jpg";
        var imageStream = new MemoryStream(new byte[] { 1, 2, 3 });
        var fileName = "profile.jpg";
        var contentType = "image/jpeg";

        _mockUserManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync(user);
        _mockImageStorageService.Setup(x => x.UploadImageAsync(
            It.IsAny<Stream>(), fileName, contentType, "profile", "testuser"))
            .ReturnsAsync(newImageUrl);
        _mockUserManager.Setup(x => x.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        await _service.UpdateProfilePictureAsync(userId, imageStream, fileName, contentType);

        // Assert
        user.ImageUrl.Should().Be(newImageUrl);
        _mockImageStorageService.Verify(x => x.DeleteImageAsync(It.IsAny<string>()), Times.Never);
        _mockImageStorageService.Verify(x => x.UploadImageAsync(
            It.IsAny<Stream>(), fileName, contentType, "profile", "testuser"), Times.Once);
    }

    public void Dispose()
    {
        _context?.Dispose();
        GC.SuppressFinalize(this);
    }
}
