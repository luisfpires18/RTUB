using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing product reservations
/// </summary>
public class ProductReservationService : IProductReservationService
{
    private readonly ApplicationDbContext _context;

    public ProductReservationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ProductReservation?> GetByIdAsync(int id)
    {
        return await _context.ProductReservations
            .Include(r => r.Product)
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<IEnumerable<ProductReservation>> GetByProductIdAsync(int productId)
    {
        return await _context.ProductReservations
            .Include(r => r.User)
            .Where(r => r.ProductId == productId)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<ProductReservation>> GetByUserIdAsync(string userId)
    {
        return await _context.ProductReservations
            .Include(r => r.Product)
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<ProductReservation?> GetByProductAndUserAsync(int productId, string userId)
    {
        return await _context.ProductReservations
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

        _context.ProductReservations.Add(reservation);
        await _context.SaveChangesAsync();
        return reservation;
    }

    public async Task DeleteAsync(int id)
    {
        var reservation = await _context.ProductReservations.FindAsync(id);
        if (reservation != null)
        {
            _context.ProductReservations.Remove(reservation);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> HasReservationAsync(int productId, string userId)
    {
        return await _context.ProductReservations
            .AnyAsync(r => r.ProductId == productId && r.UserId == userId);
    }
}
