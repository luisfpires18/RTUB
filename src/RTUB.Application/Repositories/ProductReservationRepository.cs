using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for ProductReservation entity
/// </summary>
public class ProductReservationRepository : Repository<ProductReservation>, IProductReservationRepository
{
    public ProductReservationRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<IEnumerable<ProductReservation>> GetByUserIdAsync(string userId)
    {
        using var context = CreateContext();
        return await context.Set<ProductReservation>()
            .AsNoTracking()
            .Include(pr => pr.Product)
            .Where(pr => pr.UserId == userId)
            .OrderByDescending(pr => pr.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<ProductReservation>> GetByProductIdAsync(int productId)
    {
        using var context = CreateContext();
        return await context.Set<ProductReservation>()
            .AsNoTracking()
            .Include(pr => pr.User)
            .Where(pr => pr.ProductId == productId)
            .OrderByDescending(pr => pr.CreatedAt)
            .ToListAsync();
    }
}
