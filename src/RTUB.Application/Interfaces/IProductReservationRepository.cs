using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for ProductReservation entity
/// Provides data access operations for product reservations
/// </summary>
public interface IProductReservationRepository : IRepository<ProductReservation>
{
    /// <summary>
    /// Gets product reservations by user ID with product details
    /// </summary>
    Task<IEnumerable<ProductReservation>> GetByUserIdAsync(string userId);
    
    /// <summary>
    /// Gets product reservations by product ID with user details
    /// </summary>
    Task<IEnumerable<ProductReservation>> GetByProductIdAsync(int productId);
}
