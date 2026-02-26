using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Repositories;

/// <summary>
/// Base repository implementation providing common data access operations.
/// Uses IDbContextFactory to create short-lived DbContext instances per operation,
/// eliminating EF Core tracking conflicts in Blazor Server circuits.
/// </summary>
/// <typeparam name="T">The entity type</typeparam>
public class Repository<T> : IRepository<T> where T : class
{
    protected readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    /// <summary>
    /// Backward-compatible context for derived repositories that access _context directly.
    /// Prefer using CreateContext() for isolated operations to avoid tracking conflicts.
    /// </summary>
    protected readonly ApplicationDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
        _context = contextFactory.CreateDbContext();
        _dbSet = _context.Set<T>();
    }

    /// <summary>Creates a fresh short-lived DbContext. Caller must dispose.</summary>
    protected ApplicationDbContext CreateContext() => _contextFactory.CreateDbContext();

    public virtual async Task<T?> GetByIdAsync(int id)
    {
        using var context = CreateContext();
        return await context.Set<T>().FindAsync(id).ConfigureAwait(false);
    }

    public virtual async Task<IEnumerable<T>> GetAllAsync()
    {
        using var context = CreateContext();
        return await context.Set<T>().AsNoTracking().ToListAsync().ConfigureAwait(false);
    }

    public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
    {
        using var context = CreateContext();
        return await context.Set<T>().AsNoTracking().Where(predicate).ToListAsync().ConfigureAwait(false);
    }

    public virtual async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
    {
        using var context = CreateContext();
        return await context.Set<T>().AsNoTracking().FirstOrDefaultAsync(predicate).ConfigureAwait(false);
    }

    public virtual async Task<T> AddAsync(T entity)
    {
        using var context = CreateContext();
        await context.Set<T>().AddAsync(entity).ConfigureAwait(false);
        await context.SaveChangesAsync().ConfigureAwait(false);
        return entity;
    }

    public virtual async Task AddRangeAsync(IEnumerable<T> entities)
    {
        using var context = CreateContext();
        await context.Set<T>().AddRangeAsync(entities).ConfigureAwait(false);
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    public virtual async Task UpdateAsync(T entity)
    {
        using var context = CreateContext();
        // Use Entry().State instead of DbSet.Update() to avoid traversing the entity graph.
        // Update() marks ALL navigation properties (e.g., User) as Modified, causing
        // unwanted UPDATE statements on related tables (e.g., AspNetUsers) and
        // DbUpdateConcurrencyException from stale ConcurrencyStamps.
        context.Entry(entity).State = EntityState.Modified;
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    public virtual async Task DeleteAsync(int id)
    {
        using var context = CreateContext();
        var entity = await context.Set<T>().FindAsync(id).ConfigureAwait(false);
        if (entity != null)
        {
            context.Set<T>().Remove(entity);
            await context.SaveChangesAsync().ConfigureAwait(false);
        }
    }

    public virtual async Task DeleteAsync(T entity)
    {
        using var context = CreateContext();
        // Use Entry().State instead of Remove() to avoid traversing navigation properties.
        context.Entry(entity).State = EntityState.Deleted;
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    public virtual async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null)
    {
        using var context = CreateContext();
        var dbSet = context.Set<T>();
        return predicate == null
            ? await dbSet.CountAsync().ConfigureAwait(false)
            : await dbSet.CountAsync(predicate).ConfigureAwait(false);
    }

    public virtual async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate)
    {
        using var context = CreateContext();
        return await context.Set<T>().AnyAsync(predicate).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a queryable for complex queries (read-only, no-tracking).
    /// IMPORTANT: The returned IQueryable must be materialized (ToList, First, etc.)
    /// before the caller disposes the context. Prefer using this within a
    /// using var context = CreateContext() block in derived repositories.
    /// </summary>
    /// <returns>IQueryable for the entity type (no-tracking)</returns>
    public virtual IQueryable<T> Query()
    {
        // NOTE: This creates a context that won't be disposed automatically.
        // Derived repos should prefer CreateContext() + context.Set<T>().AsNoTracking() directly.
        var context = CreateContext();
        return context.Set<T>().AsNoTracking().AsQueryable();
    }

    public virtual async Task<int> SaveChangesAsync()
    {
        // With factory pattern, each operation creates its own context and saves within that scope.
        // This method is kept for interface compatibility but should not be called directly.
        // Individual operations handle their own SaveChanges.
        return 0;
    }

    /// <summary>
    /// Reloads an entity from the database, refreshing all property values.
    /// With the factory pattern, prefer re-querying with a fresh context instead.
    /// </summary>
    public virtual async Task ReloadAsync(T entity)
    {
        using var context = CreateContext();
        context.Set<T>().Attach(entity);
        await context.Entry(entity).ReloadAsync().ConfigureAwait(false);
    }
}
