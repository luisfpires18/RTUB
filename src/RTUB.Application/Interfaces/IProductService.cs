using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for product/shop management
/// </summary>
public interface IProductService
{
    Task<Product?> GetByIdAsync(int id);
    Task<IEnumerable<Product>> GetAllAsync();
    Task<IEnumerable<Product>> GetAvailableAsync();
    Task<IEnumerable<Product>> GetPublicAsync();
    Task<IEnumerable<Product>> GetByTypeAsync(string type);
    Task<Product> CreateAsync(Product product);
    Task UpdateAsync(Product product);
    Task DeleteAsync(int id);
    Task<Dictionary<string, int>> GetTypeStatsAsync();
    Task<decimal> GetTotalInventoryValueAsync();
}
