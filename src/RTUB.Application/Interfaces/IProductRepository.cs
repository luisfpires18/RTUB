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
}
