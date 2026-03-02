using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Repositories;

/// <summary>
/// Base repository implementation providing common data access operations.
/// Uses IDbContextFactory to create short-lived DbContext instances per operation,
/// following Microsoft's recommended "context per operation" pattern for Blazor Server.
/// Each method creates a fresh DbContext, performs its work, and disposes it.
/// </summary>
/// <typeparam name="T">The entity type</typeparam>
public class Repository<T> : IRepository<T> where T : class
{
    protected readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public Repository(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    /// <summary>Creates a fresh short-lived DbContext. Caller must dispose.</summary>
    protected ApplicationDbContext CreateContext() => _contextFactory.CreateDbContext();

    /// <summary>
    /// Gets the primary key values of an entity using EF Core model metadata.
    /// Works for single-key (int, string) and composite-key entities.
    /// </summary>
    protected static object[] GetPrimaryKeyValues(ApplicationDbContext context, T entity)
    {
        var entityType = context.Model.FindEntityType(typeof(T))
            ?? throw new InvalidOperationException($"Entity type {typeof(T).Name} is not registered in the DbContext model.");
        var primaryKey = entityType.FindPrimaryKey()
            ?? throw new InvalidOperationException($"Entity type {typeof(T).Name} has no primary key defined.");
        return primaryKey.Properties
            .Select(p => p.PropertyInfo?.GetValue(entity)
                ?? p.FieldInfo?.GetValue(entity)
                ?? throw new InvalidOperationException($"Could not read primary key property '{p.Name}' from entity {typeof(T).Name}."))
            .ToArray();
    }

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

    /// <summary>
    /// Updates an existing entity using the fetch-then-SetValues pattern.
    /// Creates a fresh context, loads the tracked entity by PK, copies scalar
    /// property values from the detached entity, and saves. This ensures the
    /// entity is always tracked by the same context that performs the save.
    /// </summary>
    public virtual async Task UpdateAsync(T entity)
    {
        using var context = CreateContext();
        var keyValues = GetPrimaryKeyValues(context, entity);
        var tracked = await context.Set<T>().FindAsync(keyValues).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Entity {typeof(T).Name} with key [{string.Join(", ", keyValues)}] not found in the database.");
        context.Entry(tracked).CurrentValues.SetValues(entity);
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

    /// <summary>
    /// Deletes an entity by loading it fresh from the database using its primary key,
    /// then removing the tracked instance. Safe for detached entities.
    /// </summary>
    public virtual async Task DeleteAsync(T entity)
    {
        using var context = CreateContext();
        var keyValues = GetPrimaryKeyValues(context, entity);
        var tracked = await context.Set<T>().FindAsync(keyValues).ConfigureAwait(false);
        if (tracked != null)
        {
            context.Set<T>().Remove(tracked);
            await context.SaveChangesAsync().ConfigureAwait(false);
        }
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
    /// Executes a query with a properly scoped and disposed DbContext.
    /// The <paramref name="queryFunc"/> receives an <see cref="IQueryable{T}"/> (with AsNoTracking)
    /// and must materialize it (e.g., ToListAsync, FirstOrDefaultAsync, CountAsync).
    /// Replaces the old Query() method that leaked the underlying DbContext.
    /// </summary>
    public virtual async Task<TResult> QueryAsync<TResult>(Func<IQueryable<T>, Task<TResult>> queryFunc, CancellationToken cancellationToken = default)
    {
        using var context = CreateContext();
        return await queryFunc(context.Set<T>().AsNoTracking()).ConfigureAwait(false);
    }

    /// <summary>
    /// This method is a no-op in the context-per-operation pattern.
    /// Each CRUD method creates its own context and saves within that scope.
    /// Callers should use UpdateAsync(entity) instead of modifying and calling SaveChangesAsync().
    /// TODO: Remove from IRepository<T> once all callers are migrated.
    /// </summary>
    public virtual Task<int> SaveChangesAsync()
    {
        return Task.FromResult(0);
    }

    /// <summary>
    /// Reloads an entity from the database by re-fetching it with a fresh context
    /// and copying the current database values onto the provided entity instance.
    /// </summary>
    public virtual async Task ReloadAsync(T entity)
    {
        using var context = CreateContext();
        var keyValues = GetPrimaryKeyValues(context, entity);
        var fresh = await context.Set<T>().FindAsync(keyValues).ConfigureAwait(false);
        if (fresh != null)
        {
            // Copy fresh DB values onto the caller's entity reference
            context.Entry(fresh).CurrentValues.SetValues(entity);
            // Now copy the DB values back to the passed-in entity
            var freshValues = context.Entry(fresh).CurrentValues;
            var entityType = context.Model.FindEntityType(typeof(T))!;
            foreach (var property in entityType.GetProperties())
            {
                if (property.PropertyInfo != null)
                {
                    var dbValue = freshValues[property];
                    property.PropertyInfo.SetValue(entity, dbValue);
                }
            }
        }
    }
}
