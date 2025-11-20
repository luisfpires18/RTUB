using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for Product entity
/// </summary>
public interface IProductRepository : IRepository<Product>
{
    /// <summary>
    /// Get all products ordered by type and name
    /// </summary>
    Task<IEnumerable<Product>> GetAllOrderedAsync();

    /// <summary>
    /// Get available products
    /// </summary>
    Task<IEnumerable<Product>> GetAvailableAsync();

    /// <summary>
    /// Get public products (visible to non-members)
    /// </summary>
    Task<IEnumerable<Product>> GetPublicAsync();

    /// <summary>
    /// Get products by type (string)
    /// </summary>
    Task<IEnumerable<Product>> GetByTypeAsync(string type);
}
