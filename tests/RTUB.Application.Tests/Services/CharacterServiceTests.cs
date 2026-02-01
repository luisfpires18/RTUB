using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for CharacterService
/// Tests character retrieval, creation, and update operations
/// </summary>
public class CharacterServiceTests : IDisposable
{
    private readonly Mock<ICharacterRepository> _mockCharacterRepository;
    private readonly Mock<IInventoryRepository> _mockInventoryRepository;
    private readonly CharacterService _service;
    private readonly ApplicationDbContext _mockDbContext;

    public CharacterServiceTests()
    {
        _mockCharacterRepository = new Mock<ICharacterRepository>();
        _mockInventoryRepository = new Mock<IInventoryRepository>();

        // Create a minimal in-memory DbContext for the service
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        _mockDbContext = new ApplicationDbContext(
            options,
            Mock.Of<IHttpContextAccessor>(),
            new RTUB.Application.Services.AuditContext(),
            new RTUB.Application.Services.AuditLogAppender());

        _service = new CharacterService(
            _mockCharacterRepository.Object, 
            _mockDbContext,
            _mockInventoryRepository.Object);
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
        result.HP.Should().Be(100);
        result.Power.Should().Be(10);
        result.Speed.Should().Be(10);
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

    public void Dispose()
    {
        _mockDbContext?.Dispose();
    }
}
