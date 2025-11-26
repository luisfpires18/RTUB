using Microsoft.EntityFrameworkCore;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing shop products
/// </summary>
public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;
    private readonly IImageStorageService _imageStorageService;

    public ProductService(IProductRepository productRepository, IImageStorageService imageStorageService)
    {
        _productRepository = productRepository;
        _imageStorageService = imageStorageService;
    }

    public async Task<Product?> GetByIdAsync(int id)
    {
        return await _productRepository.GetByIdAsync(id);
    }

    public async Task<IEnumerable<Product>> GetAllAsync()
    {
        return await _productRepository.GetAllOrderedAsync();
    }

    public async Task<IEnumerable<Product>> GetAvailableAsync()
    {
        return await _productRepository.GetAvailableAsync();
    }

    public async Task<IEnumerable<Product>> GetPublicAsync()
    {
        return await _productRepository.GetPublicAsync();
    }

    public async Task<IEnumerable<Product>> GetByTypeAsync(string type)
    {
        return await _productRepository.GetByTypeAsync(type);
    }

    public async Task<Product> CreateAsync(Product product)
    {
        return await _productRepository.AddAsync(product);
    }

    public async Task UpdateAsync(Product product)
    {
        var existingProduct = await _productRepository.GetByIdAsync(product.Id);
        if (existingProduct == null)
            throw new EntityNotFoundException(nameof(Product), product.Id);

        existingProduct.Update(product.Name, product.Type, product.Price, product.Stock, product.Description);
        existingProduct.SetAvailability(product.IsAvailable);
        existingProduct.SetPublicVisibility(product.IsPublic);

        await _productRepository.UpdateAsync(existingProduct);
    }

    public async Task DeleteAsync(int id)
    {
        var product = await _productRepository.GetByIdAsync(id);
        if (product != null)
        {
            // Delete associated image from R2 storage if it exists
            if (!string.IsNullOrEmpty(product.ImageUrl))
            {
                await _imageStorageService.DeleteImageAsync(product.ImageUrl);
            }

            await _productRepository.DeleteAsync(id);
        }
    }

    public async Task<Dictionary<string, int>> GetTypeStatsAsync()
    {
        return await _productRepository.Query()
            .AsNoTracking()
            .GroupBy(p => p.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Type, x => x.Count);
    }

    public async Task<decimal> GetTotalInventoryValueAsync()
    {
        return await _productRepository.Query()
            .AsNoTracking()
            .SumAsync(p => p.Price * p.Stock);
    }
}
