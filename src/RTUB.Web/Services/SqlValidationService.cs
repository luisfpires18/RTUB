using System.Text.RegularExpressions;
using RTUB.Application.Interfaces;

namespace RTUB.Web.Services;

/// <summary>
/// Implementation of SQL validation service that ensures queries are safe for execution.
/// </summary>
public class SqlValidationService : ISqlValidationService
{
    /// <summary>
    /// Keywords forbidden in SELECT queries (anything that writes or changes schema).
    /// </summary>
    private static readonly string[] SelectForbiddenKeywords = new[]
    {
        "INSERT", "UPDATE", "DELETE", "MERGE", "DROP", "ALTER",
        "TRUNCATE", "EXEC", "EXECUTE", "CREATE", "GRANT", "REVOKE"
    };

    /// <summary>
    /// Keywords forbidden in modification queries (DDL and privilege operations).
    /// </summary>
    private static readonly string[] ModifyForbiddenKeywords = new[]
    {
        "DROP", "ALTER", "TRUNCATE", "CREATE", "GRANT", "REVOKE",
        "EXEC", "EXECUTE", "MERGE", "SELECT"
    };

    private static readonly Regex[] SelectForbiddenPatterns = SelectForbiddenKeywords
        .Select(k => new Regex($@"\b{k}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled))
        .ToArray();

    private static readonly Regex[] ModifyForbiddenPatterns = ModifyForbiddenKeywords
        .Select(k => new Regex($@"\b{k}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled))
        .ToArray();

    private static readonly string[] AllowedDmlKeywords = { "INSERT", "UPDATE", "DELETE" };

    /// <inheritdoc />
    public SqlValidationResult ValidateQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new SqlValidationResult(false, "Query cannot be empty.");

        var normalizedQuery = query.Trim();

        if (!normalizedQuery.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            return new SqlValidationResult(false, "Only SELECT queries are allowed. Query must start with SELECT.");

        for (int i = 0; i < SelectForbiddenPatterns.Length; i++)
        {
            if (SelectForbiddenPatterns[i].IsMatch(normalizedQuery))
                return new SqlValidationResult(false, $"Query contains forbidden keyword: {SelectForbiddenKeywords[i]}. Only read-only SELECT queries are allowed.");
        }

        return new SqlValidationResult(true, string.Empty);
    }

    /// <inheritdoc />
    public SqlValidationResult ValidateModifyQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new SqlValidationResult(false, "Query cannot be empty.");

        var normalizedQuery = query.Trim();

        var startsWithDml = AllowedDmlKeywords.Any(k =>
            normalizedQuery.StartsWith(k, StringComparison.OrdinalIgnoreCase));

        if (!startsWithDml)
            return new SqlValidationResult(false, "Only INSERT, UPDATE, and DELETE queries are allowed. Query must start with INSERT, UPDATE, or DELETE.");

        for (int i = 0; i < ModifyForbiddenPatterns.Length; i++)
        {
            if (ModifyForbiddenPatterns[i].IsMatch(normalizedQuery))
                return new SqlValidationResult(false, $"Query contains forbidden keyword: {ModifyForbiddenKeywords[i]}. Only INSERT, UPDATE, and DELETE queries are allowed.");
        }

        return new SqlValidationResult(true, string.Empty);
    }
}
