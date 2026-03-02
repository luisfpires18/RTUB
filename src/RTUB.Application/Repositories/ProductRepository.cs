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

    public async Task<IEnumerable<Product>> GetAvailableAsync()
    {
        using var context = CreateContext();
        return await context.Set<Product>()
            .AsNoTracking()
            .Where(p => p.IsAvailable)
            .OrderBy(p => p.Type)
            .ThenBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Product>> GetPublicAsync()
    {
        using var context = CreateContext();
        return await context.Set<Product>()
            .AsNoTracking()
            .Where(p => p.IsPublic && p.IsAvailable)
            .OrderBy(p => p.Type)
            .ThenBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Product>> GetByTypeAsync(string type)
    {
        using var context = CreateContext();
        return await context.Set<Product>()
            .AsNoTracking()
            .Where(p => p.Type == type)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }
}
