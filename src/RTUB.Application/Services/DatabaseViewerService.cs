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
    private readonly ApplicationDbContext _context;

    /// <summary>
    /// Validates that a table name contains only safe characters to prevent SQL injection.
    /// Allows alphanumeric characters and underscores only.
    /// </summary>
    private static readonly Regex SafeTableNamePattern = new(@"^\w+$", RegexOptions.Compiled);

    public DatabaseViewerService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<string>> GetTableNamesAsync(CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(
            _context.Model.GetEntityTypes()
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

        var entityType = _context.Model.GetEntityTypes()
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

        var entityType = _context.Model.GetEntityTypes()
            .FirstOrDefault(e => (e.GetTableName() ?? e.ClrType.Name) == tableName);

        if (entityType == null)
        {
            throw new ArgumentException($"Table '{tableName}' not found.", nameof(tableName));
        }

        var tableNameEscaped = entityType.GetTableName() ?? tableName;
        var countQuery = $"SELECT COUNT(*) FROM \"{tableNameEscaped}\"";

        using var command = _context.Database.GetDbConnection().CreateCommand();
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

        using var command = _context.Database.GetDbConnection().CreateCommand();
        command.CommandText = query;

        if (command.Connection?.State != ConnectionState.Open)
        {
            await command.Connection!.OpenAsync(cancellationToken);
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
        return await _context.Database.ExecuteSqlRawAsync(query, cancellationToken);
    }

    public async Task<List<Dictionary<string, object?>>> GetTableDataAsync(string tableName, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        if (!IsValidTableName(tableName))
        {
            throw new ArgumentException("Invalid table name.", nameof(tableName));
        }

        var entityType = _context.Model.GetEntityTypes()
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
