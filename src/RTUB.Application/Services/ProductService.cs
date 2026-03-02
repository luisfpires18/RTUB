using Microsoft.EntityFrameworkCore;
using RTUB.Application.Extensions;
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

    /// <summary>
    /// Initializes a new instance of the ProductService
    /// </summary>
    /// <param name="productRepository">Repository for product operations</param>
    /// <param name="imageStorageService">Service for image storage operations</param>
    public ProductService(IProductRepository productRepository, IImageStorageService imageStorageService)
    {
        _productRepository = productRepository;
        _imageStorageService = imageStorageService;
    }

    /// <summary>
    /// Gets a product by its ID
    /// </summary>
    /// <param name="id">The ID of the product to retrieve</param>
    /// <returns>The product if found, null otherwise</returns>
    public async Task<Product?> GetByIdAsync(int id)
    {
        return await _productRepository.GetByIdAsync(id);
    }

    /// <summary>
    /// Gets all products ordered by name
    /// </summary>
    /// <returns>Collection of all products</returns>
    public async Task<IEnumerable<Product>> GetAllAsync()
    {
        return await _productRepository.GetAllOrderedAsync();
    }

    /// <summary>
    /// Gets all available products (in stock and available)
    /// </summary>
    /// <returns>Collection of available products</returns>
    public async Task<IEnumerable<Product>> GetAvailableAsync()
    {
        return await _productRepository.GetAvailableAsync();
    }

    /// <summary>
    /// Gets all public products
    /// </summary>
    /// <returns>Collection of public products</returns>
    public async Task<IEnumerable<Product>> GetPublicAsync()
    {
        return await _productRepository.GetPublicAsync();
    }

    /// <summary>
    /// Gets all products of a specific type
    /// </summary>
    /// <param name="type">The product type to filter by</param>
    /// <returns>Collection of products of the specified type</returns>
    public async Task<IEnumerable<Product>> GetByTypeAsync(string type)
    {
        return await _productRepository.GetByTypeAsync(type);
    }

    /// <summary>
    /// Creates a new product
    /// </summary>
    /// <param name="product">The product entity to create</param>
    /// <returns>The created product</returns>
    public async Task<Product> CreateAsync(Product product)
    {
        return await _productRepository.AddAsync(product);
    }

    /// <summary>
    /// Updates an existing product
    /// </summary>
    /// <param name="product">The product entity with updated values</param>
    /// <exception cref="EntityNotFoundException">Thrown when the product is not found</exception>
    public async Task UpdateAsync(Product product)
    {
        var existingProduct = await _productRepository.GetByIdOrThrowAsync(product.Id);

        existingProduct.Update(product.Name, product.Type, product.Price, product.Stock, product.Description);
        existingProduct.SetAvailability(product.IsAvailable);
        existingProduct.SetPublicVisibility(product.IsPublic);

        // Update image URL if it has changed
        if (existingProduct.ImageUrl != product.ImageUrl)
        {
            existingProduct.ImageUrl = product.ImageUrl;
        }

        await _productRepository.UpdateAsync(existingProduct);
    }

    /// <summary>
    /// Deletes a product and its associated image
    /// </summary>
    /// <param name="id">The ID of the product to delete</param>
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

    /// <summary>
    /// Gets statistics about products grouped by type
    /// </summary>
    /// <returns>Dictionary mapping product types to their counts</returns>
    public async Task<Dictionary<string, int>> GetTypeStatsAsync()
    {
        return await _productRepository.QueryAsync(q => q
            .GroupBy(p => p.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Type, x => x.Count));
    }

    /// <summary>
    /// Calculates the total inventory value (sum of price * stock for all products)
    /// </summary>
    /// <returns>The total inventory value</returns>
    public async Task<decimal> GetTotalInventoryValueAsync()
    {
        return await _productRepository.QueryAsync(q => q
            .SumAsync(p => p.Price * p.Stock));
    }
}
