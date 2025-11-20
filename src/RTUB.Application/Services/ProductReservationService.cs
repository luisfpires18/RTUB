using Microsoft.EntityFrameworkCore;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing product reservations using Repository pattern
/// Now depends on IProductReservationRepository abstraction instead of concrete DbContext
/// </summary>
public class ProductReservationService : IProductReservationService
{
    private readonly IProductReservationRepository _productReservationRepository;

    public ProductReservationService(IProductReservationRepository productReservationRepository)
    {
        _productReservationRepository = productReservationRepository;
    }

    public async Task<ProductReservation?> GetByIdAsync(int id)
    {
        return await _productReservationRepository.GetByIdAsync(id);
    }

    public async Task<IEnumerable<ProductReservation>> GetByProductIdAsync(int productId)
    {
        return await _productReservationRepository.GetByProductIdAsync(productId);
    }

    public async Task<IEnumerable<ProductReservation>> GetByUserIdAsync(string userId)
    {
        return await _productReservationRepository.GetByUserIdAsync(userId);
    }

    public async Task<ProductReservation?> GetByProductAndUserAsync(int productId, string userId)
    {
        return await _productReservationRepository.Query()
            .FirstOrDefaultAsync(r => r.ProductId == productId && r.UserId == userId);
    }

    public async Task<ProductReservation> CreateAsync(ProductReservation reservation)
    {
        // Check if user already has a reservation for this product
        var existing = await GetByProductAndUserAsync(reservation.ProductId, reservation.UserId);
        if (existing != null)
        {
            throw new InvalidOperationException("Já existe uma reserva para este produto");
        }

        return await _productReservationRepository.AddAsync(reservation);
    }

    public async Task DeleteAsync(int id)
    {
        var reservation = await _productReservationRepository.GetByIdAsync(id);
        if (reservation != null)
        {
            await _productReservationRepository.DeleteAsync(reservation);
        }
    }

    public async Task<bool> HasReservationAsync(int productId, string userId)
    {
        return await _productReservationRepository.AnyAsync(r => r.ProductId == productId && r.UserId == userId);
    }
}
