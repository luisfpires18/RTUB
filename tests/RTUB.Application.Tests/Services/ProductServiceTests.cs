using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for ProductService - Shop product management
/// HIGH PRIORITY - Phase 1 Critical Service
/// </summary>
public class ProductServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IImageStorageService> _imageStorageServiceMock;
    private readonly ProductService _service;

    public ProductServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _imageStorageServiceMock = new Mock<IImageStorageService>();
        _service = new ProductService(_context, _imageStorageServiceMock.Object);
    }

    [Fact]
    public async Task CreateAsync_WithValidProduct_CreatesProduct()
    {
        var product = Product.Create("T-Shirt RTUB", "Clothing", 15.00m, 50);
        var result = await _service.CreateAsync(product);
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.Name.Should().Be("T-Shirt RTUB");
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ReturnsProduct()
    {
        var product = Product.Create("CD Album", "Music", 10.00m, 20);
        _context.Products.Add(product);
        await _context.SaveChangesAsync();
        
        var result = await _service.GetByIdAsync(product.Id);
        result.Should().NotBeNull();
        result!.Name.Should().Be("CD Album");
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistingId_ReturnsNull()
    {
        var result = await _service.GetByIdAsync(999);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllProducts_OrderedByTypeAndName()
    {
        _context.Products.AddRange(
            Product.Create("T-Shirt", "Clothing", 15.00m, 10),
            Product.Create("Hoodie", "Clothing", 30.00m, 5),
            Product.Create("CD", "Music", 10.00m, 20)
        );
        await _context.SaveChangesAsync();
        
        var result = await _service.GetAllAsync();
        result.Should().HaveCount(3);
        result.First().Type.Should().Be("Clothing");
    }

    [Fact]
    public async Task GetAvailableAsync_ReturnsOnlyAvailableProducts()
    {
        var available = Product.Create("Available", "Type1", 10.00m, 10);
        var unavailable = Product.Create("Unavailable", "Type2", 20.00m, 0);
        unavailable.SetAvailability(false);
        _context.Products.AddRange(available, unavailable);
        await _context.SaveChangesAsync();
        
        var result = await _service.GetAvailableAsync();
        result.Should().HaveCount(1);
        result.First().IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task GetPublicAsync_ReturnsOnlyPublicAndAvailableProducts()
    {
        var publicAvailable = Product.Create("Public", "Type1", 10.00m, 10);
        var publicUnavailable = Product.Create("Unavailable", "Type2", 20.00m, 0);
        publicUnavailable.SetAvailability(false);
        var privateProduct = Product.Create("Private", "Type3", 15.00m, 5);
        privateProduct.SetPublicVisibility(false);
        _context.Products.AddRange(publicAvailable, publicUnavailable, privateProduct);
        await _context.SaveChangesAsync();
        
        var result = await _service.GetPublicAsync();
        result.Should().HaveCount(1);
        result.First().IsPublic.Should().BeTrue();
    }

    [Fact]
    public async Task GetByTypeAsync_WithValidType_ReturnsProductsOfType()
    {
        _context.Products.AddRange(
            Product.Create("T-Shirt", "Clothing", 15.00m, 10),
            Product.Create("Hoodie", "Clothing", 30.00m, 5),
            Product.Create("CD", "Music", 10.00m, 20)
        );
        await _context.SaveChangesAsync();
        
        var result = await _service.GetByTypeAsync("Clothing");
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(p => p.Type.Should().Be("Clothing"));
    }

    [Fact]
    public async Task GetByTypeAsync_WithNonExistingType_ReturnsEmptyList()
    {
        var result = await _service.GetByTypeAsync("NonExistent");
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_WithValidProduct_UpdatesProductAndInvalidatesCache()
    {
        var product = Product.Create("Original", "Type1", 10.00m, 10);
        _context.Products.Add(product);
        await _context.SaveChangesAsync();
        
        product.Update("Updated", "Type1", 15.00m, 10);
        await _service.UpdateAsync(product);
        
        var updated = await _context.Products.FindAsync(product.Id);
        updated!.Name.Should().Be("Updated");
        updated.Price.Should().Be(15.00m);
    }

    [Fact]
    public async Task DeleteAsync_WithExistingProduct_DeletesProduct()
    {
        var product = Product.Create("To Delete", "Type1", 10.00m, 10);
        _context.Products.Add(product);
        await _context.SaveChangesAsync();
        
        await _service.DeleteAsync(product.Id);
        var deleted = await _context.Products.FindAsync(product.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistingProduct_DoesNotThrow()
    {
        var act = async () => await _service.DeleteAsync(999);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeleteAsync_WithProductHavingImage_DeletesImageFromStorage()
    {
        var product = Product.Create("Product with Image", "Type1", 10.00m, 10);
        product.ImageUrl = "https://example.com/images/product.jpg";
        _context.Products.Add(product);
        await _context.SaveChangesAsync();
        
        await _service.DeleteAsync(product.Id);
        
        _imageStorageServiceMock.Verify(x => x.DeleteImageAsync("https://example.com/images/product.jpg"), Times.Once);
        var deleted = await _context.Products.FindAsync(product.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_WithProductWithoutImage_DoesNotCallImageStorageService()
    {
        var product = Product.Create("Product without Image", "Type1", 10.00m, 10);
        _context.Products.Add(product);
        await _context.SaveChangesAsync();
        
        await _service.DeleteAsync(product.Id);
        
        _imageStorageServiceMock.Verify(x => x.DeleteImageAsync(It.IsAny<string>()), Times.Never);
        var deleted = await _context.Products.FindAsync(product.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task GetTypeStatsAsync_ReturnsCorrectGroupedCounts()
    {
        _context.Products.AddRange(
            Product.Create("T-Shirt", "Clothing", 15.00m, 10),
            Product.Create("Hoodie", "Clothing", 30.00m, 5),
            Product.Create("CD", "Music", 10.00m, 20),
            Product.Create("Vinyl", "Music", 25.00m, 10),
            Product.Create("Poster", "Merchandise", 5.00m, 50)
        );
        await _context.SaveChangesAsync();
        
        var result = await _service.GetTypeStatsAsync();
        result.Should().HaveCount(3);
        result["Clothing"].Should().Be(2);
        result["Music"].Should().Be(2);
        result["Merchandise"].Should().Be(1);
    }

    [Fact]
    public async Task GetTypeStatsAsync_WithNoProducts_ReturnsEmptyDictionary()
    {
        var result = await _service.GetTypeStatsAsync();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTotalInventoryValueAsync_CalculatesCorrectTotal()
    {
        _context.Products.AddRange(
            Product.Create("Product1", "Type1", 10.00m, 5),  // 50
            Product.Create("Product2", "Type2", 20.00m, 3),  // 60
            Product.Create("Product3", "Type3", 15.00m, 2)   // 30
        );
        await _context.SaveChangesAsync();
        
        var result = await _service.GetTotalInventoryValueAsync();
        result.Should().Be(140.00m);
    }

    [Fact]
    public async Task GetTotalInventoryValueAsync_WithNoProducts_ReturnsZero()
    {
        var result = await _service.GetTotalInventoryValueAsync();
        result.Should().Be(0.00m);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
