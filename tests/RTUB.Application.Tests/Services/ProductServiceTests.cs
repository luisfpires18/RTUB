using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Repositories;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for ProductService - Shop product management
/// HIGH PRIORITY - Phase 1 Critical Service
/// </summary>
public class ProductServiceTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly Mock<IImageStorageService> _imageStorageServiceMock;
    private readonly ProductService _service;

    public ProductServiceTests(DatabaseFixture fixture)
    {
        // Clean database at constructor start to ensure test isolation
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();

        _fixture = fixture;
        _context = _fixture.CreateContext();
        _imageStorageServiceMock = new Mock<IImageStorageService>();
        _service = new ProductService(new ProductRepository(_fixture.CreateContextFactory()), _imageStorageServiceMock.Object);
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

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task UpdateAsync_SavesAvailableAllYears_BothWays(bool before, bool after)
    {
        var product = Product.Create("CD", "Album", 10.00m, 5);
        product.AvailableAllYears = before;
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        product.AvailableAllYears = after;
        await _service.UpdateAsync(product);

        _context.ChangeTracker.Clear();
        var saved = await _context.Products.FindAsync(product.Id);
        (saved!.AvailableAllYears, saved.Price, saved.Stock, saved.CreatedAt).Should().Be((after, 10.00m, 5, product.CreatedAt));
    }

    [Fact]
    public async Task DeleteAsync_WithExistingProduct_DeletesProduct()
    {
        var product = Product.Create("To Delete", "Type1", 10.00m, 10);
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        await _service.DeleteAsync(product.Id);
        _context.ChangeTracker.Clear();
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
        _context.ChangeTracker.Clear();
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
        _context.ChangeTracker.Clear();
        var deleted = await _context.Products.FindAsync(product.Id);
        deleted.Should().BeNull();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
