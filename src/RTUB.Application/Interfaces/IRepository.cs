using System.Linq.Expressions;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Generic repository interface for data access operations
/// Provides common CRUD operations and query capabilities for entities
/// Follows Repository pattern and Dependency Inversion Principle
/// </summary>
/// <typeparam name="T">The entity type</typeparam>
public interface IRepository<T> where T : class
{
    /// <summary>
    /// Gets an entity by its ID
    /// </summary>
    /// <param name="id">The entity ID</param>
    /// <returns>The entity if found, otherwise null</returns>
    Task<T?> GetByIdAsync(int id);

    /// <summary>
    /// Gets all entities
    /// </summary>
    /// <returns>Collection of all entities</returns>
    Task<IEnumerable<T>> GetAllAsync();

    /// <summary>
    /// Finds entities matching the predicate
    /// </summary>
    /// <param name="predicate">Filter expression</param>
    /// <returns>Collection of matching entities</returns>
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);

    /// <summary>
    /// Gets the first entity matching the predicate, or null
    /// </summary>
    /// <param name="predicate">Filter expression</param>
    /// <returns>First matching entity or null</returns>
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate);

    /// <summary>
    /// Adds a new entity
    /// </summary>
    /// <param name="entity">Entity to add</param>
    /// <returns>The added entity</returns>
    Task<T> AddAsync(T entity);

    /// <summary>
    /// Adds multiple entities
    /// </summary>
    /// <param name="entities">Entities to add</param>
    Task AddRangeAsync(IEnumerable<T> entities);

    /// <summary>
    /// Updates an existing entity
    /// </summary>
    /// <param name="entity">Entity to update</param>
    Task UpdateAsync(T entity);

    /// <summary>
    /// Deletes an entity by ID
    /// </summary>
    /// <param name="id">The entity ID</param>
    Task DeleteAsync(int id);

    /// <summary>
    /// Deletes an entity
    /// </summary>
    /// <param name="entity">Entity to delete</param>
    Task DeleteAsync(T entity);

    /// <summary>
    /// Counts entities matching the predicate
    /// </summary>
    /// <param name="predicate">Optional filter expression</param>
    /// <returns>Count of matching entities</returns>
    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null);

    /// <summary>
    /// Checks if any entity matches the predicate
    /// </summary>
    /// <param name="predicate">Filter expression</param>
    /// <returns>True if any entity matches, otherwise false</returns>
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate);

    /// <summary>
    /// Executes a query with a properly scoped and disposed DbContext.
    /// The <paramref name="queryFunc"/> receives an <see cref="IQueryable{T}"/> (with AsNoTracking)
    /// and must materialize it (e.g., ToListAsync, FirstOrDefaultAsync, CountAsync).
    /// </summary>
    /// <typeparam name="TResult">The materialized result type</typeparam>
    /// <param name="queryFunc">Function that builds and executes the query</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<TResult> QueryAsync<TResult>(Func<IQueryable<T>, Task<TResult>> queryFunc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reloads an entity from the database, overwriting any local changes
    /// and resetting the change tracker state for this entity.
    /// </summary>
    /// <param name="entity">The entity to reload</param>
    Task ReloadAsync(T entity);
}
