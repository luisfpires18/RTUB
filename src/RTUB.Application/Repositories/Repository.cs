using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Repositories;

/// <summary>
/// Base repository implementation providing common data access operations
/// Implements Repository pattern with generic CRUD operations
/// </summary>
/// <typeparam name="T">The entity type</typeparam>
public class Repository<T> : IRepository<T> where T : class
{
    protected readonly ApplicationDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(ApplicationDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(int id)
    {
        return await _dbSet.FindAsync(id).ConfigureAwait(false);
    }

    public virtual async Task<IEnumerable<T>> GetAllAsync()
    {
        return await _dbSet.AsNoTracking().ToListAsync().ConfigureAwait(false);
    }

    public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.AsNoTracking().Where(predicate).ToListAsync().ConfigureAwait(false);
    }

    public virtual async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.AsNoTracking().FirstOrDefaultAsync(predicate).ConfigureAwait(false);
    }

    public virtual async Task<T> AddAsync(T entity)
    {
        await _dbSet.AddAsync(entity).ConfigureAwait(false);
        await SaveChangesAsync().ConfigureAwait(false);
        return entity;
    }

    public virtual async Task AddRangeAsync(IEnumerable<T> entities)
    {
        await _dbSet.AddRangeAsync(entities).ConfigureAwait(false);
        await SaveChangesAsync().ConfigureAwait(false);
    }

    public virtual async Task UpdateAsync(T entity)
    {
        // Attach the entity if it's not being tracked
        var entry = _context.Entry(entity);
        if (entry.State == EntityState.Detached)
        {
            _dbSet.Attach(entity);
            entry.State = EntityState.Modified;
        }

        await SaveChangesAsync().ConfigureAwait(false);
    }

    public virtual async Task DeleteAsync(int id)
    {
        var entity = await _dbSet.FindAsync(id).ConfigureAwait(false);
        if (entity != null)
        {
            _dbSet.Remove(entity);
            await SaveChangesAsync().ConfigureAwait(false);
        }
    }

    public virtual async Task DeleteAsync(T entity)
    {
        // If entity is detached (loaded with AsNoTracking), attach it first
        var entry = _context.Entry(entity);
        if (entry.State == EntityState.Detached)
        {
            _dbSet.Attach(entity);
        }

        _dbSet.Remove(entity);
        await SaveChangesAsync().ConfigureAwait(false);
    }

    public virtual async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null)
    {
        return predicate == null
            ? await _dbSet.CountAsync().ConfigureAwait(false)
            : await _dbSet.CountAsync(predicate).ConfigureAwait(false);
    }

    public virtual async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.AnyAsync(predicate).ConfigureAwait(false);
    }

    public virtual IQueryable<T> Query()
    {
        return _dbSet.AsQueryable();
    }

    public virtual async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync().ConfigureAwait(false);
    }
}
