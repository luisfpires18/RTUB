using RTUB.Application.Interfaces;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Extensions;

/// <summary>
/// Extension methods for IRepository to reduce repetitive null-check patterns
/// </summary>
public static class RepositoryExtensions
{
    /// <summary>
    /// Gets an entity by ID or throws EntityNotFoundException if not found.
    /// Reduces boilerplate code in services that need to ensure an entity exists.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <param name="repository">The repository instance</param>
    /// <param name="id">The entity ID</param>
    /// <returns>The entity if found</returns>
    /// <exception cref="EntityNotFoundException">Thrown when entity with the given ID is not found</exception>
    public static async Task<T> GetByIdOrThrowAsync<T>(this IRepository<T> repository, int id) where T : class
    {
        var entity = await repository.GetByIdAsync(id);
        if (entity == null)
        {
            var entityName = typeof(T).Name;
            throw new EntityNotFoundException(entityName, id);
        }
        return entity;
    }
}
