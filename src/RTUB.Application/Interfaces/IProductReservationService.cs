using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for product reservation management
/// </summary>
public interface IProductReservationService
{
    Task<ProductReservation?> GetByIdAsync(int id);
    Task<IEnumerable<ProductReservation>> GetByProductIdAsync(int productId);
    Task<IEnumerable<ProductReservation>> GetByUserIdAsync(string userId);
    Task<ProductReservation?> GetByProductAndUserAsync(int productId, string userId);
    Task<ProductReservation> CreateAsync(ProductReservation reservation);
    Task DeleteAsync(int id);
    Task<bool> HasReservationAsync(int productId, string userId);
}
