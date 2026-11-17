// Owner tools: database tables, select and modify queries.
using System.Data.Common;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

public partial class OwnerOperationsService
{
    // ---------- /owner/db ----------

    public async Task<IReadOnlyList<string>> GetTablesAsync() => await _database.GetTableNamesAsync();

    public async Task<EventResult<OwnerDbGrid>> GetTableAsync(string table, int? page, int? pageSize)
    {
        var size = TablePageSizes.Contains(pageSize ?? 0) ? pageSize!.Value : 50;
        var current = Math.Max(1, page ?? 1);
        try
        {
            var total = await _database.GetTableRecordCountAsync(table);
            var columns = await _database.GetTableColumnsAsync(table);
            var rows = await _database.GetTableDataAsync(table, current, size);
            return EventResult<OwnerDbGrid>.Ok(Grid(columns, rows, total, current, size));
        }
        catch (ArgumentException)
        {
            return EventResult<OwnerDbGrid>.Fail(EventResultStatus.NotFound);
        }
    }

    public async Task<EventResult<OwnerDbGrid>> RunSelectAsync(OwnerDbQueryInput input)
    {
        var validation = _sql.ValidateQuery(input.Query ?? string.Empty);
        if (!validation.IsValid)
        {
            return EventResult<OwnerDbGrid>.Invalid("query", validation.ErrorMessage);
        }

        try
        {
            var rows = await _database.ExecuteSelectQueryAsync(input.Query!);
            var columns = rows.Count > 0 ? rows[0].Keys.ToList() : [];
            return EventResult<OwnerDbGrid>.Ok(Grid(columns, rows, rows.Count, 1, Math.Max(rows.Count, 1)));
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException)
        {
            return EventResult<OwnerDbGrid>.Invalid("query", $"Erro ao executar a consulta: {ex.Message}");
        }
    }

    public async Task<EventResult<OwnerDbModifyResult>> RunModifyAsync(OwnerDbModifyInput input)
    {
        var validation = _sql.ValidateModifyQuery(input.Query ?? string.Empty);
        if (!validation.IsValid)
        {
            return EventResult<OwnerDbModifyResult>.Invalid("query", validation.ErrorMessage);
        }

        if (!input.Confirmed)
        {
            return EventResult<OwnerDbModifyResult>.Invalid("confirmed", "Confirme que quer executar esta alteração: não pode ser desfeita.");
        }

        try
        {
            return EventResult<OwnerDbModifyResult>.Ok(new OwnerDbModifyResult(await _database.ExecuteModifyQueryAsync(input.Query!)));
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException or ArgumentException)
        {
            return EventResult<OwnerDbModifyResult>.Invalid("query", $"Erro ao executar a consulta: {ex.Message}");
        }
    }

    private static OwnerDbGrid Grid(List<string> columns, List<Dictionary<string, object?>> rows, int total, int page, int pageSize) =>
        new(columns, rows.Select(r => (IReadOnlyList<string>)columns.Select(c => Cell(r.GetValueOrDefault(c))).ToList()).ToList(),
            total, page, pageSize);

    /// <summary>How the old grid showed a value.</summary>
    internal static string Cell(object? value) => value switch
    {
        null => "[NULL]",
        byte[] bytes => $"[Binary: {bytes.Length} bytes]",
        DateTime date => date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) is { } text ? text.Length > 100 ? text[..100] + "..." : text : "",
    };

    private static void Require(Dictionary<string, string[]> errors, string field, string? value, int max, string missing)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = [missing];
        }
        else if (value.Length > max)
        {
            errors[field] = [$"Máximo de {max} caracteres."];
        }
    }

    /// <summary>The first <paramref name="max"/> characters, back to the last whole word when one ends past half.</summary>
    private static string WordCut(string text, int max)
    {
        var cut = text[..max];
        var space = cut.LastIndexOf(' ');
        return (space > max / 2 ? cut[..space] : cut).TrimEnd(' ', ',', ';', ':', '.');
    }
}
