using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for Product entity
/// </summary>
public class ProductRepository : Repository<Product>, IProductRepository
{
    public ProductRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<IEnumerable<Product>> GetAllOrderedAsync()
    {
        using var context = CreateContext();
        return await context.Set<Product>()
            .AsNoTracking()
            .OrderBy(p => p.Type)
            .ThenBy(p => p.Name)
            .ToListAsync();
    }
}
