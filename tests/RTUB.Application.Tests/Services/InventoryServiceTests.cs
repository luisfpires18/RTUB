using FluentAssertions;
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
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for InventoryService
/// Tests beer inventory management and healing functionality
/// </summary>
public class InventoryServiceTests : IDisposable
{
    private readonly Mock<IInventoryRepository> _inventoryRepositoryMock;
    private readonly Mock<ICharacterRepository> _characterRepositoryMock;
    private readonly Mock<ILogger<InventoryService>> _loggerMock;
    private readonly IInventoryService _inventoryService;
    private readonly ApplicationDbContext _dbContext;

    public InventoryServiceTests()
    {
        _inventoryRepositoryMock = new Mock<IInventoryRepository>();
        _characterRepositoryMock = new Mock<ICharacterRepository>();
        _loggerMock = new Mock<ILogger<InventoryService>>();

        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        var userManagerMock = new Mock<UserManager<ApplicationUser>>(userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var config = Options.Create(new MyTunoScalingConfiguration());

        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"InventoryTests_{Guid.NewGuid()}")
            .Options;
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        var auditContext = new AuditContext();
        var auditLogAppenderMock = new Mock<IAuditLogAppender>();
        _dbContext = new ApplicationDbContext(dbOptions, httpContextAccessorMock.Object, auditContext, auditLogAppenderMock.Object);

        _inventoryService = new InventoryService(
            _inventoryRepositoryMock.Object,
            _characterRepositoryMock.Object,
            userManagerMock.Object,
            _loggerMock.Object,
            config,
            _dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task UseBeerAsync_WithValidInput_ShouldRestoreHP()
    {
        // Arrange
        var userId = "user1";
        var character = Character.Create(userId);

        // Set character HP to 60 out of 100 (TotalHP is based on base HP)
        // Default base HP is 100, so TotalHP should be 100 at level 1
        character.TakeDamage(40); // CurrentHP = 60

        var beerItem = InventoryItem.Create(userId, InventoryItemType.Beer, 5);

        _inventoryRepositoryMock
            .Setup(r => r.GetItemAsync(userId, InventoryItemType.Beer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(beerItem);

        _characterRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId))
            .ReturnsAsync(character);

        _characterRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Character>()))
            .Returns(Task.FromResult(character));

        _inventoryRepositoryMock
            .Setup(r => r.ConsumeItemAsync(userId, InventoryItemType.Beer, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Expected heal amount: 25% of TotalHP (100) = 25 HP
        var expectedHealAmount = 25;
        var expectedNewHP = 85; // 60 + 25 = 85

        // Act
        var result = await _inventoryService.UseBeerAsync(userId);

        // Assert
        result.Success.Should().BeTrue();
        result.HealedAmount.Should().Be(expectedHealAmount);
        result.Message.Should().Contain("Personagem curado!");
        result.Message.Should().Contain($"+{expectedHealAmount} HP");

        character.CurrentHP.Should().Be(expectedNewHP);

        // Verify repository methods were called correctly
        _inventoryRepositoryMock.Verify(
            r => r.GetItemAsync(userId, InventoryItemType.Beer, It.IsAny<CancellationToken>()),
            Times.Once);

        _characterRepositoryMock.Verify(
            r => r.GetByUserIdAsync(userId),
            Times.Once);

        _characterRepositoryMock.Verify(
            r => r.UpdateAsync(It.Is<Character>(c => c.CurrentHP == expectedNewHP)),
            Times.Once);

        _inventoryRepositoryMock.Verify(
            r => r.ConsumeItemAsync(userId, InventoryItemType.Beer, 1, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UseBeerAsync_WithNoBeer_ShouldReturnFailure()
    {
        // Arrange
        var userId = "user1";
        var character = Character.Create(userId);
        character.TakeDamage(40); // CurrentHP = 60

        var beerItem = InventoryItem.Create(userId, InventoryItemType.Beer, 0);

        _inventoryRepositoryMock
            .Setup(r => r.GetItemAsync(userId, InventoryItemType.Beer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(beerItem);

        var initialHP = character.CurrentHP;

        // Act
        var result = await _inventoryService.UseBeerAsync(userId);

        // Assert
        result.Success.Should().BeFalse();
        result.HealedAmount.Should().Be(0);
        result.Message.Should().Be("Não tens cervejas no inventário");

        character.CurrentHP.Should().Be(initialHP); // HP should not change

        // Verify character repository was never called
        _characterRepositoryMock.Verify(
            r => r.GetByUserIdAsync(It.IsAny<string>()),
            Times.Never);

        _characterRepositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<Character>()),
            Times.Never);

        _inventoryRepositoryMock.Verify(
            r => r.ConsumeItemAsync(It.IsAny<string>(), It.IsAny<InventoryItemType>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UseBeerAsync_WithNullBeerItem_ShouldReturnFailure()
    {
        // Arrange
        var userId = "user1";

        _inventoryRepositoryMock
            .Setup(r => r.GetItemAsync(userId, InventoryItemType.Beer, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InventoryItem?)null); // No beer item exists

        // Act
        var result = await _inventoryService.UseBeerAsync(userId);

        // Assert
        result.Success.Should().BeFalse();
        result.HealedAmount.Should().Be(0);
        result.Message.Should().Be("Não tens cervejas no inventário");

        // Verify character repository was never called
        _characterRepositoryMock.Verify(
            r => r.GetByUserIdAsync(It.IsAny<string>()),
            Times.Never);

        _characterRepositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<Character>()),
            Times.Never);

        _inventoryRepositoryMock.Verify(
            r => r.ConsumeItemAsync(It.IsAny<string>(), It.IsAny<InventoryItemType>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UseBeerAsync_WithFullHP_ShouldReturnFailure()
    {
        // Arrange
        var userId = "user1";
        var character = Character.Create(userId);
        // Character at full HP (CurrentHP is null, meaning full HP)

        var beerItem = InventoryItem.Create(userId, InventoryItemType.Beer, 5);

        _inventoryRepositoryMock
            .Setup(r => r.GetItemAsync(userId, InventoryItemType.Beer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(beerItem);

        _characterRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId))
            .ReturnsAsync(character);

        var initialQuantity = beerItem.Quantity;

        // Act
        var result = await _inventoryService.UseBeerAsync(userId);

        // Assert
        result.Success.Should().BeFalse();
        result.HealedAmount.Should().Be(0);
        result.Message.Should().Be("O personagem já está com HP máximo");

        beerItem.Quantity.Should().Be(initialQuantity); // Beer should not be consumed

        // Verify character was not updated and beer was not consumed
        _characterRepositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<Character>()),
            Times.Never);

        _inventoryRepositoryMock.Verify(
            r => r.ConsumeItemAsync(It.IsAny<string>(), It.IsAny<InventoryItemType>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UseBeerAsync_WithCurrentHPEqualsToTotalHP_ShouldReturnFailure()
    {
        // Arrange
        var userId = "user1";
        var character = Character.Create(userId);
        character.RestoreHP(); // Explicitly set CurrentHP to TotalHP

        var beerItem = InventoryItem.Create(userId, InventoryItemType.Beer, 5);

        _inventoryRepositoryMock
            .Setup(r => r.GetItemAsync(userId, InventoryItemType.Beer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(beerItem);

        _characterRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId))
            .ReturnsAsync(character);

        // Act
        var result = await _inventoryService.UseBeerAsync(userId);

        // Assert
        result.Success.Should().BeFalse();
        result.HealedAmount.Should().Be(0);
        result.Message.Should().Be("O personagem já está com HP máximo");

        // Verify character was not updated and beer was not consumed
        _characterRepositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<Character>()),
            Times.Never);

        _inventoryRepositoryMock.Verify(
            r => r.ConsumeItemAsync(It.IsAny<string>(), It.IsAny<InventoryItemType>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UseBeerAsync_WithNoCharacter_ShouldReturnFailure()
    {
        // Arrange
        var userId = "user1";

        var beerItem = InventoryItem.Create(userId, InventoryItemType.Beer, 5);

        _inventoryRepositoryMock
            .Setup(r => r.GetItemAsync(userId, InventoryItemType.Beer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(beerItem);

        _characterRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId))
            .ReturnsAsync((Character?)null); // No character exists

        // Act
        var result = await _inventoryService.UseBeerAsync(userId);

        // Assert
        result.Success.Should().BeFalse();
        result.HealedAmount.Should().Be(0);
        result.Message.Should().Be("Personagem não encontrado");

        // Verify character was not updated and beer was not consumed
        _characterRepositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<Character>()),
            Times.Never);

        _inventoryRepositoryMock.Verify(
            r => r.ConsumeItemAsync(It.IsAny<string>(), It.IsAny<InventoryItemType>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UseBeerAsync_WhenConsumeItemFails_ShouldReturnFailure()
    {
        // Arrange
        var userId = "user1";
        var character = Character.Create(userId);
        character.TakeDamage(40); // CurrentHP = 60

        var beerItem = InventoryItem.Create(userId, InventoryItemType.Beer, 5);

        _inventoryRepositoryMock
            .Setup(r => r.GetItemAsync(userId, InventoryItemType.Beer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(beerItem);

        _characterRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId))
            .ReturnsAsync(character);

        _characterRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Character>()))
            .Returns(Task.FromResult(character));

        _inventoryRepositoryMock
            .Setup(r => r.ConsumeItemAsync(userId, InventoryItemType.Beer, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // Consume operation fails

        // Act
        var result = await _inventoryService.UseBeerAsync(userId);

        // Assert
        result.Success.Should().BeFalse();
        result.HealedAmount.Should().Be(0);
        result.Message.Should().Be("Erro ao consumir cerveja");

        // Verify character was updated (healing happened before consume check)
        _characterRepositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<Character>()),
            Times.Once);
    }

    [Fact]
    public async Task UseBeerAsync_WithHealingOverflowToMax_ShouldCapAtMaxHP()
    {
        // Arrange
        var userId = "user1";
        var character = Character.Create(userId);

        // Set character HP to 95 out of 100
        // When healed by 25 HP, should cap at 100 (not 120)
        character.TakeDamage(5); // CurrentHP = 95

        var beerItem = InventoryItem.Create(userId, InventoryItemType.Beer, 1);

        _inventoryRepositoryMock
            .Setup(r => r.GetItemAsync(userId, InventoryItemType.Beer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(beerItem);

        _characterRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId))
            .ReturnsAsync(character);

        _characterRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Character>()))
            .Returns(Task.FromResult(character));

        _inventoryRepositoryMock
            .Setup(r => r.ConsumeItemAsync(userId, InventoryItemType.Beer, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var expectedHealAmount = 25;
        var expectedNewHP = character.TotalHP; // Should cap at max HP (100)

        // Act
        var result = await _inventoryService.UseBeerAsync(userId);

        // Assert
        result.Success.Should().BeTrue();
        result.HealedAmount.Should().Be(expectedHealAmount);
        character.CurrentHP.Should().Be(expectedNewHP);
        character.CurrentHP.Should().BeLessThanOrEqualTo(character.TotalHP);
    }

    [Fact]
    public async Task UseBeerAsync_WithLeveledCharacter_ShouldHealBasedOnScaledHP()
    {
        // Arrange
        var userId = "user1";
        var character = Character.Create(userId);

        // Level up character to increase TotalHP
        character.AddXP(500); // This should level up the character

        var totalHP = character.TotalHP;
        var damageAmount = totalHP / 2; // Half HP
        character.TakeDamage(damageAmount);

        var beerItem = InventoryItem.Create(userId, InventoryItemType.Beer, 1);

        _inventoryRepositoryMock
            .Setup(r => r.GetItemAsync(userId, InventoryItemType.Beer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(beerItem);

        _characterRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId))
            .ReturnsAsync(character);

        _characterRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Character>()))
            .Returns(Task.FromResult(character));

        _inventoryRepositoryMock
            .Setup(r => r.ConsumeItemAsync(userId, InventoryItemType.Beer, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Expected heal amount: 25% of TotalHP (scaled with level)
        var expectedHealAmount = (int)Math.Round(totalHP * 0.25);

        // Act
        var result = await _inventoryService.UseBeerAsync(userId);

        // Assert
        result.Success.Should().BeTrue();
        result.HealedAmount.Should().Be(expectedHealAmount);
        result.Message.Should().Contain($"+{expectedHealAmount} HP");
    }

    [Fact]
    public async Task GetBeerQuantityAsync_WithBeer_ShouldReturnQuantity()
    {
        // Arrange
        var userId = "user1";
        var expectedQuantity = 7;

        var beerItem = InventoryItem.Create(userId, InventoryItemType.Beer, expectedQuantity);

        _inventoryRepositoryMock
            .Setup(r => r.GetItemAsync(userId, InventoryItemType.Beer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(beerItem);

        // Act
        var quantity = await _inventoryService.GetBeerQuantityAsync(userId);

        // Assert
        quantity.Should().Be(expectedQuantity);

        _inventoryRepositoryMock.Verify(
            r => r.GetItemAsync(userId, InventoryItemType.Beer, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetBeerQuantityAsync_WithNoBeer_ShouldReturnZero()
    {
        // Arrange
        var userId = "user1";

        _inventoryRepositoryMock
            .Setup(r => r.GetItemAsync(userId, InventoryItemType.Beer, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InventoryItem?)null); // No beer item exists

        // Act
        var quantity = await _inventoryService.GetBeerQuantityAsync(userId);

        // Assert
        quantity.Should().Be(0);

        _inventoryRepositoryMock.Verify(
            r => r.GetItemAsync(userId, InventoryItemType.Beer, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetBeerQuantityAsync_WithZeroQuantityBeer_ShouldReturnZero()
    {
        // Arrange
        var userId = "user1";

        var beerItem = InventoryItem.Create(userId, InventoryItemType.Beer, 0);

        _inventoryRepositoryMock
            .Setup(r => r.GetItemAsync(userId, InventoryItemType.Beer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(beerItem);

        // Act
        var quantity = await _inventoryService.GetBeerQuantityAsync(userId);

        // Assert
        quantity.Should().Be(0);

        _inventoryRepositoryMock.Verify(
            r => r.GetItemAsync(userId, InventoryItemType.Beer, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UseBeerAsync_ShouldLogWarning_WhenNoCharacter()
    {
        // Arrange
        var userId = "user1";

        var beerItem = InventoryItem.Create(userId, InventoryItemType.Beer, 5);

        _inventoryRepositoryMock
            .Setup(r => r.GetItemAsync(userId, InventoryItemType.Beer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(beerItem);

        _characterRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId))
            .ReturnsAsync((Character?)null);

        // Act
        await _inventoryService.UseBeerAsync(userId);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"User {userId} attempted to use beer but has no character")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task UseBeerAsync_ShouldLogError_WhenConsumeItemFails()
    {
        // Arrange
        var userId = "user1";
        var character = Character.Create(userId);
        character.TakeDamage(40);

        var beerItem = InventoryItem.Create(userId, InventoryItemType.Beer, 5);

        _inventoryRepositoryMock
            .Setup(r => r.GetItemAsync(userId, InventoryItemType.Beer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(beerItem);

        _characterRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId))
            .ReturnsAsync(character);

        _characterRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Character>()))
            .Returns(Task.FromResult(character));

        _inventoryRepositoryMock
            .Setup(r => r.ConsumeItemAsync(userId, InventoryItemType.Beer, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await _inventoryService.UseBeerAsync(userId);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Failed to consume beer for user {userId} even though quantity was checked")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
