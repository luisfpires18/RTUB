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
    public ProductRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Product>> GetAllOrderedAsync()
    {
        return await _dbSet
            .AsNoTracking()
            .OrderBy(p => p.Type)
            .ThenBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Product>> GetAvailableAsync()
    {
        return await _dbSet
            .AsNoTracking()
            .Where(p => p.IsAvailable)
            .OrderBy(p => p.Type)
            .ThenBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Product>> GetPublicAsync()
    {
        return await _dbSet
            .AsNoTracking()
            .Where(p => p.IsPublic && p.IsAvailable)
            .OrderBy(p => p.Type)
            .ThenBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Product>> GetByTypeAsync(string type)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(p => p.Type == type)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }
}
