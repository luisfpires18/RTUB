using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace RTUB.Application.Extensions;

/// <summary>
/// Extension methods for <see cref="DatabaseFacade"/> providing safe transaction handling
/// across different EF Core providers.
/// </summary>
public static class DatabaseFacadeExtensions
{
    /// <summary>
    /// Begins a transaction if the provider supports it (e.g., SQLite, SQL Server).
    /// Returns null for providers that don't support transactions (e.g., InMemory).
    /// Use with: <c>if (transaction != null) await transaction.CommitAsync(ct);</c>
    /// </summary>
    public static async Task<IDbContextTransaction?> BeginTransactionIfSupportedAsync(
        this DatabaseFacade database, CancellationToken cancellationToken = default)
    {
        try
        {
            return await database.BeginTransactionAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // InMemory provider does not support transactions
            return null;
        }
    }
}
