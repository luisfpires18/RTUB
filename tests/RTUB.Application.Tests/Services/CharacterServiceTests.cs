using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Configuration;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for CharacterService
/// Tests character retrieval, creation, and update operations
/// </summary>
public class CharacterServiceTests
{
    private readonly Mock<ICharacterRepository> _mockCharacterRepository;
    private readonly CharacterService _service;

    public CharacterServiceTests()
    {
        _mockCharacterRepository = new Mock<ICharacterRepository>();

        // Create a minimal in-memory DbContext for the service
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        var mockUserStore = new Mock<IUserStore<ApplicationUser>>();
        var mockUserManager = new Mock<UserManager<ApplicationUser>>(
            mockUserStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var mockConfig = Options.Create(new MyTunoScalingConfiguration());
        var mockLogger = new Mock<ILogger<CharacterService>>();
        var dbContext = new ApplicationDbContext(options, Mock.Of<IHttpContextAccessor>(), new AuditContext(), new AuditLogAppender());

        _service = new CharacterService(
            _mockCharacterRepository.Object,
            mockUserManager.Object,
            mockConfig,
            mockLogger.Object,
            WrapInFactory(dbContext),
            Mock.Of<IWebHostEnvironment>());
    }

    #region GetOrCreateCharacterAsync Tests

    [Fact]
    public async Task GetOrCreateCharacterAsync_WithExistingCharacter_ShouldReturnExisting()
    {
        // Arrange
        var userId = "user-123";
        var existingCharacter = Character.Create(userId);
        existingCharacter.Id = 1;
        existingCharacter.Level = 5;
        existingCharacter.XP = 50;

        _mockCharacterRepository
            .Setup(r => r.GetByUserIdAsync(userId))
            .ReturnsAsync(existingCharacter);

        // Act
        var result = await _service.GetOrCreateCharacterAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(existingCharacter);
        result.Id.Should().Be(1);
        result.Level.Should().Be(5);
        result.XP.Should().Be(50);
        _mockCharacterRepository.Verify(r => r.GetByUserIdAsync(userId), Times.Once);
        _mockCharacterRepository.Verify(r => r.AddAsync(It.IsAny<Character>()), Times.Never);
    }

    [Fact]
    public async Task GetOrCreateCharacterAsync_WithNonExistingCharacter_ShouldCreateNew()
    {
        // Arrange
        var userId = "user-123";

        _mockCharacterRepository
            .Setup(r => r.GetByUserIdAsync(userId))
            .ReturnsAsync((Character?)null);

        Character? addedCharacter = null;
        _mockCharacterRepository
            .Setup(r => r.AddAsync(It.IsAny<Character>()))
            .Returns<Character>(async c =>
            {
                addedCharacter = c;
                c.Id = 1; // Simulate EF Core assigning ID
                return await Task.FromResult(c);
            });

        // Act
        var result = await _service.GetOrCreateCharacterAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be(userId);
        result.Level.Should().Be(1);
        result.XP.Should().Be(0);
        result.HP.Should().Be(MyTunoScaling.BaseHp);
        result.Power.Should().Be(MyTunoScaling.BasePower);
        result.Speed.Should().Be(MyTunoScaling.BaseSpeed);
        _mockCharacterRepository.Verify(r => r.GetByUserIdAsync(userId), Times.Once);
        _mockCharacterRepository.Verify(r => r.AddAsync(It.IsAny<Character>()), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task GetOrCreateCharacterAsync_WithEmptyUserId_ShouldThrowException(string? userId)
    {
        // Act
        var act = async () => await _service.GetOrCreateCharacterAsync(userId!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*User ID*");
        _mockCharacterRepository.Verify(r => r.GetByUserIdAsync(It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region GetCharacterAsync Tests

    [Fact]
    public async Task GetCharacterAsync_WithExistingCharacter_ShouldReturnCharacter()
    {
        // Arrange
        var userId = "user-123";
        var character = Character.Create(userId);
        character.Id = 1;

        _mockCharacterRepository
            .Setup(r => r.GetByUserIdAsync(userId))
            .ReturnsAsync(character);

        // Act
        var result = await _service.GetCharacterAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(character);
        _mockCharacterRepository.Verify(r => r.GetByUserIdAsync(userId), Times.Once);
    }

    [Fact]
    public async Task GetCharacterAsync_WithNonExistingCharacter_ShouldReturnNull()
    {
        // Arrange
        var userId = "user-123";

        _mockCharacterRepository
            .Setup(r => r.GetByUserIdAsync(userId))
            .ReturnsAsync((Character?)null);

        // Act
        var result = await _service.GetCharacterAsync(userId);

        // Assert
        result.Should().BeNull();
        _mockCharacterRepository.Verify(r => r.GetByUserIdAsync(userId), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task GetCharacterAsync_WithEmptyUserId_ShouldThrowException(string? userId)
    {
        // Act
        var act = async () => await _service.GetCharacterAsync(userId!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*User ID*");
        _mockCharacterRepository.Verify(r => r.GetByUserIdAsync(It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region UpdateCharacterAsync Tests

    [Fact]
    public async Task UpdateCharacterAsync_WithValidCharacter_ShouldUpdate()
    {
        // Arrange
        var character = Character.Create("user-123");
        character.Id = 1;
        character.AddXP(100);
        character.UpgradeHP();

        _mockCharacterRepository
            .Setup(r => r.UpdateAsync(character))
            .Returns(Task.CompletedTask);

        // Act
        await _service.UpdateCharacterAsync(character);

        // Assert
        _mockCharacterRepository.Verify(r => r.UpdateAsync(character), Times.Once);
    }

    [Fact]
    public async Task UpdateCharacterAsync_WithNullCharacter_ShouldThrowException()
    {
        // Act
        var act = async () => await _service.UpdateCharacterAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("character");
        _mockCharacterRepository.Verify(r => r.UpdateAsync(It.IsAny<Character>()), Times.Never);
    }

    #endregion

    #region GetDailyRewardAmount Tests

    [Fact]
    public void GetDailyRewardAmount_Level1_ShouldReturnBaseReward()
    {
        // Default config: BaseFidelis=500, PerLevelFidelis=5, BalancePercent=0.0
        var result = _service.GetDailyRewardAmount(1);
        result.Should().Be(505m); // 500 + (1 * 5) + 0 balance
    }

    [Fact]
    public void GetDailyRewardAmount_Level50_ShouldScaleWithLevel()
    {
        var result = _service.GetDailyRewardAmount(50);
        result.Should().Be(750m); // 500 + (50 * 5) + 0 balance
    }

    [Fact]
    public void GetDailyRewardAmount_Level0_ShouldReturnBase()
    {
        var result = _service.GetDailyRewardAmount(0);
        result.Should().Be(500m); // 500 + (0 * 5) + 0 balance
    }

    [Fact]
    public void GetDailyRewardAmount_WithBalance_ShouldIncludePercentBonus()
    {
        // Default config: BalancePercent=0.0 (no balance bonus in v5)
        var result = _service.GetDailyRewardAmount(1, 10_000m);
        result.Should().Be(505m); // 500 + (1 * 5) + (10000 * 0 = 0)
    }

    #endregion

    #region ClaimDailyRewardAsync Tests

    [Fact]
    public async Task ClaimDailyRewardAsync_FirstClaim_ShouldSucceed()
    {
        // Arrange
        var userId = "user-123";
        var testUser = new ApplicationUser { Id = userId, FidelisBalance = 100m, LastDailyRewardClaim = null };

        var mockUserStore = new Mock<IUserStore<ApplicationUser>>();
        var mockUserManager = new Mock<UserManager<ApplicationUser>>(
            mockUserStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(testUser);
        mockUserManager.Setup(m => m.UpdateAsync(testUser)).ReturnsAsync(IdentityResult.Success);

        var service = new CharacterService(
            _mockCharacterRepository.Object,
            mockUserManager.Object,
            Options.Create(new MyTunoScalingConfiguration()),
            new Mock<ILogger<CharacterService>>().Object,
            WrapInFactory(new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}").Options, Mock.Of<IHttpContextAccessor>(), new AuditContext(), new AuditLogAppender())),
            Mock.Of<IWebHostEnvironment>());

        // Act
        var (success, message, reward) = await service.ClaimDailyRewardAsync(userId, 10);

        // Assert
        success.Should().BeTrue();
        reward.Should().Be(550m); // 500 + (10 * 5) + (100 * 0.0 = 0)
        testUser.FidelisBalance.Should().Be(650m); // 100 + 550
        testUser.LastDailyRewardClaim.Should().NotBeNull();
        testUser.LastDailyRewardClaim!.Value.Date.Should().Be(DateTime.UtcNow.Date);
    }

    [Fact]
    public async Task ClaimDailyRewardAsync_AlreadyClaimedToday_ShouldFail()
    {
        // Arrange
        var userId = "user-123";
        var testUser = new ApplicationUser
        {
            Id = userId,
            FidelisBalance = 100m,
            LastDailyRewardClaim = DateTime.UtcNow // Already claimed today
        };

        var mockUserStore = new Mock<IUserStore<ApplicationUser>>();
        var mockUserManager = new Mock<UserManager<ApplicationUser>>(
            mockUserStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(testUser);

        var service = new CharacterService(
            _mockCharacterRepository.Object,
            mockUserManager.Object,
            Options.Create(new MyTunoScalingConfiguration()),
            new Mock<ILogger<CharacterService>>().Object,
            WrapInFactory(new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}").Options, Mock.Of<IHttpContextAccessor>(), new AuditContext(), new AuditLogAppender())),
            Mock.Of<IWebHostEnvironment>());

        // Act
        var (success, message, reward) = await service.ClaimDailyRewardAsync(userId, 10);

        // Assert
        success.Should().BeFalse();
        reward.Should().Be(0);
        testUser.FidelisBalance.Should().Be(100m, "balance should not change on failed claim");
        mockUserManager.Verify(m => m.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task ClaimDailyRewardAsync_ClaimedYesterday_ShouldSucceed()
    {
        // Arrange
        var userId = "user-123";
        var testUser = new ApplicationUser
        {
            Id = userId,
            FidelisBalance = 500m,
            LastDailyRewardClaim = DateTime.UtcNow.AddDays(-1)
        };

        var mockUserStore = new Mock<IUserStore<ApplicationUser>>();
        var mockUserManager = new Mock<UserManager<ApplicationUser>>(
            mockUserStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(testUser);
        mockUserManager.Setup(m => m.UpdateAsync(testUser)).ReturnsAsync(IdentityResult.Success);

        var service = new CharacterService(
            _mockCharacterRepository.Object,
            mockUserManager.Object,
            Options.Create(new MyTunoScalingConfiguration()),
            new Mock<ILogger<CharacterService>>().Object,
            WrapInFactory(new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}").Options, Mock.Of<IHttpContextAccessor>(), new AuditContext(), new AuditLogAppender())),
            Mock.Of<IWebHostEnvironment>());

        // Act
        var (success, message, reward) = await service.ClaimDailyRewardAsync(userId, 1);

        // Assert
        success.Should().BeTrue();
        reward.Should().Be(505m); // 500 + (1 * 5) + (500 * 0.0 = 0)
        testUser.FidelisBalance.Should().Be(1005m); // 500 + 505
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task ClaimDailyRewardAsync_WithEmptyUserId_ShouldThrow(string? userId)
    {
        var act = async () => await _service.ClaimDailyRewardAsync(userId!, 1);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    private static IDbContextFactory<ApplicationDbContext> WrapInFactory(ApplicationDbContext dbContext)
    {
        var mock = new Mock<IDbContextFactory<ApplicationDbContext>>();
        mock.Setup(f => f.CreateDbContext()).Returns(dbContext);
        return mock.Object;
    }
}
