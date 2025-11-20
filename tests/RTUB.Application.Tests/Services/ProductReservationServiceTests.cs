using FluentAssertions;
using Moq;
using MockQueryable.Moq;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for ProductReservationService
/// </summary>
public class ProductReservationServiceTests
{
    private readonly Mock<IProductReservationRepository> _mockRepository;
    private readonly ProductReservationService _service;

    public ProductReservationServiceTests()
    {
        _mockRepository = new Mock<IProductReservationRepository>();
        _service = new ProductReservationService(_mockRepository.Object);
    }

    [Fact]
    public async Task CreateAsync_WithValidReservation_CreatesReservation()
    {
        // Arrange
        var reservation = ProductReservation.Create(1, "user123", "TestUser", true, "M", "Display Name");
        var emptyList = new List<ProductReservation>();
        var mockQueryable = emptyList.BuildMockDbSet().Object;
        _mockRepository.Setup(r => r.Query()).Returns(mockQueryable);
        _mockRepository.Setup(r => r.AddAsync(It.IsAny<ProductReservation>()))
            .ReturnsAsync(reservation);

        // Act
        var result = await _service.CreateAsync(reservation);

        // Assert
        result.Should().NotBeNull();
        result.ProductId.Should().Be(1);
        result.UserId.Should().Be("user123");
        result.UserNickname.Should().Be("TestUser");
        result.Size.Should().Be("M");
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateReservation_ThrowsInvalidOperationException()
    {
        // Arrange
        var existingReservation = ProductReservation.Create(1, "user123", "TestUser", false);
        var existingList = new List<ProductReservation> { existingReservation };
        var mockQueryable = existingList.BuildMockDbSet().Object;
        _mockRepository.Setup(r => r.Query()).Returns(mockQueryable);

        var reservation = ProductReservation.Create(1, "user123", "TestUser", false);

        // Act & Assert
        var act = async () => await _service.CreateAsync(reservation);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*reserva*");
    }

    [Fact]
    public async Task GetByProductIdAsync_ReturnsReservationsForProduct()
    {
        // Arrange
        var reservations = new List<ProductReservation>
        {
            ProductReservation.Create(1, "user1", "User1", false),
            ProductReservation.Create(1, "user2", "User2", true, "L")
        };
        _mockRepository.Setup(r => r.GetByProductIdAsync(1))
            .ReturnsAsync(reservations);

        // Act
        var result = await _service.GetByProductIdAsync(1);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(r => r.UserId == "user1");
        result.Should().Contain(r => r.UserId == "user2");
    }

    [Fact]
    public async Task GetByUserIdAsync_ReturnsReservationsForUser()
    {
        // Arrange
        var reservations = new List<ProductReservation>
        {
            ProductReservation.Create(1, "user1", "User1", true, "M"),
            ProductReservation.Create(2, "user1", "User1", false)
        };
        _mockRepository.Setup(r => r.GetByUserIdAsync("user1"))
            .ReturnsAsync(reservations);

        // Act
        var result = await _service.GetByUserIdAsync("user1");

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(r => r.ProductId == 1);
        result.Should().Contain(r => r.ProductId == 2);
    }

    [Fact]
    public async Task HasReservationAsync_WithExistingReservation_ReturnsTrue()
    {
        // Arrange
        _mockRepository.Setup(r => r.AnyAsync(It.IsAny<System.Linq.Expressions.Expression<Func<ProductReservation, bool>>>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.HasReservationAsync(1, "user1");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasReservationAsync_WithoutReservation_ReturnsFalse()
    {
        // Arrange
        _mockRepository.Setup(r => r.AnyAsync(It.IsAny<System.Linq.Expressions.Expression<Func<ProductReservation, bool>>>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.HasReservationAsync(1, "user1");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetByProductAndUserAsync_WithExistingReservation_ReturnsReservation()
    {
        // Arrange
        var reservation = ProductReservation.Create(1, "user1", "User1", false, null, "Custom Name");
        _mockRepository.Setup(r => r.Query())
            .Returns(new List<ProductReservation> { reservation }.BuildMockDbSet().Object);

        // Act
        var result = await _service.GetByProductAndUserAsync(1, "user1");

        // Assert
        result.Should().NotBeNull();
        result!.DisplayName.Should().Be("Custom Name");
    }

    [Fact]
    public async Task DeleteAsync_RemovesReservation()
    {
        // Arrange
        var reservation = ProductReservation.Create(1, "user1", "User1", false);
        _mockRepository.Setup(r => r.GetByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        // Act
        await _service.DeleteAsync(reservation.Id);

        // Assert
        _mockRepository.Verify(r => r.DeleteAsync(It.IsAny<ProductReservation>()), Times.Once);
    }
}
