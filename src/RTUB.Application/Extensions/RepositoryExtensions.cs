using RTUB.Application.Interfaces;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Extensions;

/// <summary>
/// Extension methods for IRepository to provide common data access patterns
/// Promotes DRY principle and consistent error handling across services
/// </summary>
public static class RepositoryExtensions
{
    /// <summary>
    /// Gets an entity by its ID or throws an exception if not found
    /// Useful when entity existence is required for the operation to continue
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <param name="repository">The repository instance</param>
    /// <param name="id">The entity ID</param>
    /// <returns>The entity if found</returns>
    /// <exception cref="EntityNotFoundException">Thrown when entity with the specified ID is not found</exception>
    public static async Task<TEntity> GetByIdOrThrowAsync<TEntity>(
        this IRepository<TEntity> repository,
        int id) where TEntity : class
    {
        var entity = await repository.GetByIdAsync(id);
        if (entity == null)
        {
            var entityName = typeof(TEntity).Name;
            throw new EntityNotFoundException(entityName, id);
        }
        return entity;
    }
}
