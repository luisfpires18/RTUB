using Microsoft.EntityFrameworkCore;

namespace RTUB.Application.Extensions;

/// <summary>
/// Extension methods for IQueryable to provide common pagination and filtering patterns
/// Promotes DRY principle and consistent query patterns across services
/// </summary>
public static class QueryableExtensions
{
    /// <summary>
    /// Applies pagination to a queryable using skip/take pattern
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <param name="query">The source query</param>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <returns>Paginated query</returns>
    public static IQueryable<T> Paginate<T>(this IQueryable<T> query, int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;

        return query
            .Skip((page - 1) * pageSize)
            .Take(pageSize);
    }

    /// <summary>
    /// Applies pagination and executes the query asynchronously
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <param name="query">The source query</param>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <returns>Paginated list of entities</returns>
    public static Task<List<T>> PaginateAsync<T>(this IQueryable<T> query, int page, int pageSize)
    {
        return query
            .Paginate(page, pageSize)
            .ToListAsync();
    }

    /// <summary>
    /// Applies a filter predicate only if the condition is true
    /// Useful for building queries with optional filters
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <param name="query">The source query</param>
    /// <param name="condition">Condition to check before applying filter</param>
    /// <param name="predicate">Filter predicate to apply</param>
    /// <returns>Filtered query if condition is true, otherwise original query</returns>
    public static IQueryable<T> WhereIf<T>(
        this IQueryable<T> query,
        bool condition,
        System.Linq.Expressions.Expression<Func<T, bool>> predicate)
    {
        return condition ? query.Where(predicate) : query;
    }

}
