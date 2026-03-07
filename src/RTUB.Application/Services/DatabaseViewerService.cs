using System.Data;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// Service for database viewer operations
/// Provides safe access to database tables and query execution for Owner-only admin tool
/// </summary>
public class DatabaseViewerService : IDatabaseViewerService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly ISqlValidationService _sqlValidationService;

    /// <summary>
    /// Validates that a table name contains only safe characters to prevent SQL injection.
    /// Allows alphanumeric characters and underscores only.
    /// </summary>
    private static readonly Regex SafeTableNamePattern = new(@"^\w+$", RegexOptions.Compiled);

    public DatabaseViewerService(IDbContextFactory<ApplicationDbContext> contextFactory, ISqlValidationService sqlValidationService)
    {
        _contextFactory = contextFactory;
        _sqlValidationService = sqlValidationService;
    }

    public async Task<List<string>> GetTableNamesAsync(CancellationToken cancellationToken = default)
    {
        using var ctx = _contextFactory.CreateDbContext();
        return await Task.FromResult(
            ctx.Model.GetEntityTypes()
                .Select(e => e.GetTableName() ?? e.ClrType.Name)
                .Where(name => !string.IsNullOrEmpty(name))
                .Distinct()
                .OrderBy(name => name)
                .ToList());
    }

    public async Task<List<string>> GetTableColumnsAsync(string tableName, CancellationToken cancellationToken = default)
    {
        if (!IsValidTableName(tableName))
        {
            throw new ArgumentException("Invalid table name.", nameof(tableName));
        }

        using var ctx = _contextFactory.CreateDbContext();
        var entityType = ctx.Model.GetEntityTypes()
            .FirstOrDefault(e => (e.GetTableName() ?? e.ClrType.Name) == tableName);

        if (entityType == null)
        {
            throw new ArgumentException($"Table '{tableName}' not found.", nameof(tableName));
        }

        return await Task.FromResult(
            entityType.GetProperties()
                .Select(p => p.GetColumnName())
                .Where(c => !string.IsNullOrEmpty(c))
                .ToList()!);
    }

    public async Task<int> GetTableRecordCountAsync(string tableName, CancellationToken cancellationToken = default)
    {
        if (!IsValidTableName(tableName))
        {
            throw new ArgumentException("Invalid table name.", nameof(tableName));
        }

        using var ctx2 = _contextFactory.CreateDbContext();
        var entityType2 = ctx2.Model.GetEntityTypes()
            .FirstOrDefault(e => (e.GetTableName() ?? e.ClrType.Name) == tableName);

        if (entityType2 == null)
        {
            throw new ArgumentException($"Table '{tableName}' not found.", nameof(tableName));
        }

        var tableNameEscaped = entityType2.GetTableName() ?? tableName;
        var countQuery = $"SELECT COUNT(*) FROM \"{tableNameEscaped}\"";

        using var command = ctx2.Database.GetDbConnection().CreateCommand();
        command.CommandText = countQuery;

        if (command.Connection?.State != ConnectionState.Open)
        {
            await command.Connection!.OpenAsync(cancellationToken);
        }

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    public async Task<List<Dictionary<string, object?>>> ExecuteSelectQueryAsync(string query, CancellationToken cancellationToken = default)
    {
        var results = new List<Dictionary<string, object?>>();

        using var ctx = _contextFactory.CreateDbContext();
        var connection = ctx.Database.GetDbConnection(); // owned by ctx; do not dispose separately
        using var command = connection.CreateCommand();
        command.CommandText = query;

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        using var reader = await command.ExecuteReaderAsync(cancellationToken);

        // Get column names from reader
        var columnNames = new List<string>();
        for (int i = 0; i < reader.FieldCount; i++)
        {
            columnNames.Add(reader.GetName(i));
        }

        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                row[columnNames[i]] = value;
            }
            results.Add(row);
        }

        return results;
    }

    public async Task<int> ExecuteModifyQueryAsync(string query, CancellationToken cancellationToken = default)
    {
        var validation = _sqlValidationService.ValidateModifyQuery(query);
        if (!validation.IsValid)
        {
            throw new ArgumentException(validation.ErrorMessage, nameof(query));
        }

        using var ctx = _contextFactory.CreateDbContext();
        return await ctx.Database.ExecuteSqlRawAsync(query, cancellationToken);
    }

    public async Task<List<Dictionary<string, object?>>> GetTableDataAsync(string tableName, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        if (!IsValidTableName(tableName))
        {
            throw new ArgumentException("Invalid table name.", nameof(tableName));
        }

        using var ctx = _contextFactory.CreateDbContext();
        var entityType = ctx.Model.GetEntityTypes()
            .FirstOrDefault(e => (e.GetTableName() ?? e.ClrType.Name) == tableName);

        if (entityType == null)
        {
            throw new ArgumentException($"Table '{tableName}' not found.", nameof(tableName));
        }

        var tableNameEscaped = entityType.GetTableName() ?? tableName;
        var offset = (page - 1) * pageSize;
        var dataQuery = $"SELECT * FROM \"{tableNameEscaped}\" LIMIT {pageSize} OFFSET {offset}";

        return await ExecuteSelectQueryAsync(dataQuery, cancellationToken);
    }

    private static bool IsValidTableName(string tableName)
    {
        return !string.IsNullOrWhiteSpace(tableName) && SafeTableNamePattern.IsMatch(tableName);
    }
}
