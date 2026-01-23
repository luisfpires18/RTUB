namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for database viewer operations
/// Provides safe access to database tables and query execution for Owner-only admin tool
/// </summary>
public interface IDatabaseViewerService
{
    /// <summary>
    /// Gets all table names from the EF Core model
    /// </summary>
    Task<List<string>> GetTableNamesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets column names for a specific table
    /// </summary>
    Task<List<string>> GetTableColumnsAsync(string tableName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total record count for a table
    /// </summary>
    Task<int> GetTableRecordCountAsync(string tableName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a SELECT query and returns results as a list of dictionaries
    /// </summary>
    Task<List<Dictionary<string, object?>>> ExecuteSelectQueryAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a modification query (INSERT/UPDATE/DELETE) and returns rows affected
    /// </summary>
    Task<int> ExecuteModifyQueryAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets paginated data from a table
    /// </summary>
    Task<List<Dictionary<string, object?>>> GetTableDataAsync(string tableName, int page, int pageSize, CancellationToken cancellationToken = default);
}
