using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
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
        DetachLocalDuplicate(entity);

        // Attach the entity if it's not being tracked
        var entry = _context.Entry(entity);
        if (entry.State == EntityState.Detached)
        {
            _dbSet.Attach(entity);
            entry.State = EntityState.Modified;
        }
        else
        {
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

    /// <summary>
    /// Gets a queryable for complex queries (read-only, no-tracking)
    /// Allows services to build custom queries while still using repository
    /// Can be combined with QueryableExtensions (Paginate, WhereIf, etc.)
    /// Note: This returns a no-tracking query for read-only operations.
    /// If you need change tracking, use GetByIdAsync or explicitly attach entities.
    /// </summary>
    /// <returns>IQueryable for the entity type (no-tracking)</returns>
    public virtual IQueryable<T> Query()
    {
        return _dbSet.AsNoTracking().AsQueryable();
    }

    public virtual async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync().ConfigureAwait(false);
    }

    private void DetachLocalDuplicate(T entity)
    {
        var entityType = _context.Model.FindEntityType(typeof(T));
        var primaryKey = entityType?.FindPrimaryKey();
        if (primaryKey == null)
        {
            return;
        }

        var keyValues = primaryKey.Properties
            .Select(p => p.PropertyInfo?.GetValue(entity))
            .ToArray();

        // Disable auto detect changes to prevent tracking navigation properties
        var wasAutoDetectChangesEnabled = _context.ChangeTracker.AutoDetectChangesEnabled;
        try
        {
            _context.ChangeTracker.AutoDetectChangesEnabled = false;

            var trackedEntry = _context.ChangeTracker
                .Entries<T>()
                .FirstOrDefault(e => e.State != EntityState.Detached && KeysMatch(primaryKey, e.Entity, keyValues));

            if (trackedEntry != null && !ReferenceEquals(trackedEntry.Entity, entity))
            {
                trackedEntry.State = EntityState.Detached;
            }
        }
        finally
        {
            _context.ChangeTracker.AutoDetectChangesEnabled = wasAutoDetectChangesEnabled;
        }
    }

    private static bool KeysMatch(IKey key, T trackedEntity, object?[] keyValues)
    {
        for (int i = 0; i < key.Properties.Count; i++)
        {
            var property = key.Properties[i];
            var trackedValue = property.PropertyInfo?.GetValue(trackedEntity);
            var incomingValue = keyValues[i];

            if (trackedValue == null && incomingValue == null)
            {
                continue;
            }

            if (trackedValue == null || incomingValue == null)
            {
                return false;
            }

            if (!trackedValue.Equals(incomingValue))
            {
                return false;
            }
        }

        return true;
    }
}
