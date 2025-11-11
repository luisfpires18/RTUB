using FluentAssertions;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for ProductReservationService
/// </summary>
public class ProductReservationServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly ProductReservationService _service;

    public ProductReservationServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options, Mock.Of<Microsoft.AspNetCore.Http.IHttpContextAccessor>(), new AuditContext());
        _service = new ProductReservationService(_context);
    }

    [Fact]
    public async Task CreateAsync_WithValidReservation_CreatesReservation()
    {
        // Arrange
        var product = Product.Create("T-Shirt", "Clothing", 15.00m, 10);
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        var reservation = ProductReservation.Create(product.Id, "user123", "TestUser", true, "M", "Display Name");

        // Act
        var result = await _service.CreateAsync(reservation);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.ProductId.Should().Be(product.Id);
        result.UserId.Should().Be("user123");
        result.UserNickname.Should().Be("TestUser");
        result.Size.Should().Be("M");
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateReservation_ThrowsInvalidOperationException()
    {
        // Arrange
        var product = Product.Create("T-Shirt", "Clothing", 15.00m, 10);
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        var reservation1 = ProductReservation.Create(product.Id, "user123", "TestUser", false);
        await _service.CreateAsync(reservation1);

        var reservation2 = ProductReservation.Create(product.Id, "user123", "TestUser", false);

        // Act & Assert
        var act = async () => await _service.CreateAsync(reservation2);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*reserva*");
    }

    [Fact]
    public async Task GetByProductIdAsync_ReturnsReservationsForProduct()
    {
        // Arrange
        var product = Product.Create("Album", "Music", 10.00m, 5);
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        var reservation1 = ProductReservation.Create(product.Id, "user1", "User1", false);
        var reservation2 = ProductReservation.Create(product.Id, "user2", "User2", true, "L");
        _context.ProductReservations.Add(reservation1);
        _context.ProductReservations.Add(reservation2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetByProductIdAsync(product.Id);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(r => r.UserId == "user1");
        result.Should().Contain(r => r.UserId == "user2");
    }

    [Fact]
    public async Task GetByUserIdAsync_ReturnsReservationsForUser()
    {
        // Arrange
        var product1 = Product.Create("T-Shirt", "Clothing", 15.00m, 10);
        var product2 = Product.Create("Album", "Music", 10.00m, 5);
        _context.Products.AddRange(product1, product2);
        await _context.SaveChangesAsync();

        var reservation1 = ProductReservation.Create(product1.Id, "user1", "User1", true, "M");
        var reservation2 = ProductReservation.Create(product2.Id, "user1", "User1", false);
        _context.ProductReservations.Add(reservation1);
        _context.ProductReservations.Add(reservation2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetByUserIdAsync("user1");

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(r => r.ProductId == product1.Id);
        result.Should().Contain(r => r.ProductId == product2.Id);
    }

    [Fact]
    public async Task HasReservationAsync_WithExistingReservation_ReturnsTrue()
    {
        // Arrange
        var product = Product.Create("Pin", "Accessory", 5.00m, 20);
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        var reservation = ProductReservation.Create(product.Id, "user1", "User1", false);
        await _service.CreateAsync(reservation);

        // Act
        var result = await _service.HasReservationAsync(product.Id, "user1");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasReservationAsync_WithoutReservation_ReturnsFalse()
    {
        // Arrange
        var product = Product.Create("Pin", "Accessory", 5.00m, 20);
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.HasReservationAsync(product.Id, "user1");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetByProductAndUserAsync_WithExistingReservation_ReturnsReservation()
    {
        // Arrange
        var product = Product.Create("Mug", "Accessory", 8.00m, 15);
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        var reservation = ProductReservation.Create(product.Id, "user1", "User1", false, null, "Custom Name");
        await _service.CreateAsync(reservation);

        // Act
        var result = await _service.GetByProductAndUserAsync(product.Id, "user1");

        // Assert
        result.Should().NotBeNull();
        result!.DisplayName.Should().Be("Custom Name");
    }

    [Fact]
    public async Task DeleteAsync_RemovesReservation()
    {
        // Arrange
        var product = Product.Create("Cap", "Clothing", 12.00m, 8);
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        var reservation = ProductReservation.Create(product.Id, "user1", "User1", false);
        await _service.CreateAsync(reservation);

        // Act
        await _service.DeleteAsync(reservation.Id);

        // Assert
        var result = await _service.GetByIdAsync(reservation.Id);
        result.Should().BeNull();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
