using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing shop products
/// </summary>
public class ProductService : IProductService
{
    private readonly ApplicationDbContext _context;
    private readonly IImageService _imageService;
    private readonly IProductStorageService _productStorageService;
    private readonly ILogger<ProductService> _logger;

    public ProductService(
        ApplicationDbContext context, 
        IImageService imageService,
        IProductStorageService productStorageService,
        ILogger<ProductService> logger)
    {
        _context = context;
        _imageService = imageService;
        _productStorageService = productStorageService;
        _logger = logger;
    }

    public async Task<Product?> GetByIdAsync(int id)
    {
        return await _context.Products.FindAsync(id);
    }

    public async Task<IEnumerable<Product>> GetAllAsync()
    {
        return await _context.Products
            .OrderBy(p => p.Type)
            .ThenBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Product>> GetAvailableAsync()
    {
        return await _context.Products
            .Where(p => p.IsAvailable)
            .OrderBy(p => p.Type)
            .ThenBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Product>> GetPublicAsync()
    {
        return await _context.Products
            .Where(p => p.IsAvailable && p.IsPublic)
            .OrderBy(p => p.Type)
            .ThenBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Product>> GetByTypeAsync(string type)
    {
        return await _context.Products
            .Where(p => p.Type == type)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<Product> CreateAsync(Product product)
    {
        _context.Products.Add(product);
        await _context.SaveChangesAsync();
        return product;
    }

    public async Task UpdateAsync(Product product)
    {
        _context.Products.Update(product);
        await _context.SaveChangesAsync();
        
        // Invalidate the cached product image so the new image is served immediately
        _imageService.InvalidateProductImageCache(product.Id);
    }

    public async Task SetProductImageAsync(int id, byte[]? imageData, string? contentType)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null)
            throw new InvalidOperationException($"Product with ID {id} not found");

        // If imageData is provided, upload to IDrive S3
        if (imageData != null && !string.IsNullOrEmpty(contentType))
        {
            // STRATEGY: Replace-Before-Upload
            // Step 1: Delete old image BEFORE uploading new one
            if (!string.IsNullOrEmpty(product.ImageUrl))
            {
                // Extract filename and delete from S3
                var oldFilename = ExtractFilenameFromUrl(product.ImageUrl);
                if (!string.IsNullOrEmpty(oldFilename))
                {
                    _logger.LogInformation("Deleting old product image from S3: {Filename}", oldFilename);
                    await _productStorageService.DeleteImageAsync(oldFilename);
                }
            }
            
            // Step 2: Upload new image to IDrive S3 and get the filename
            var filename = await _productStorageService.UploadImageAsync(imageData, id, contentType);
            
            // Step 3: Get the public URL from the storage service
            var imageUrl = await _productStorageService.GetImageUrlAsync(filename);
            
            // Step 4: Store the full URL in the ImageUrl field
            product.SetImageUrl(imageUrl);
        }
        
        _context.Products.Update(product);
        await _context.SaveChangesAsync();
        
        // Invalidate the cached product image so the new image is served immediately
        _imageService.InvalidateProductImageCache(id);
    }

    public async Task DeleteAsync(int id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product != null)
        {
            // Delete S3 image if exists
            if (!string.IsNullOrEmpty(product.ImageUrl))
            {
                // Extract filename from URL and delete from S3
                var filename = ExtractFilenameFromUrl(product.ImageUrl);
                if (!string.IsNullOrEmpty(filename))
                {
                    await _productStorageService.DeleteImageAsync(filename);
                }
            }

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<Dictionary<string, int>> GetTypeStatsAsync()
    {
        return await _context.Products
            .GroupBy(p => p.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Type, x => x.Count);
    }

    public async Task<decimal> GetTotalInventoryValueAsync()
    {
        return await _context.Products
            .SumAsync(p => p.Price * p.Stock);
    }

    private string? ExtractFilenameFromUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return null;

        try
        {
            // Extract filename from URL path (last segment after the last '/')
            var uri = new Uri(url);
            var segments = uri.AbsolutePath.Split('/');
            return segments.Length > 0 ? segments[^1] : null;
        }
        catch
        {
            return null;
        }
    }
}
