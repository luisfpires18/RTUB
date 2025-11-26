using System.Text.RegularExpressions;

namespace RTUB.Web.Services;

/// <summary>
/// Service for validating SQL queries to ensure they are safe for read-only execution.
/// </summary>
public interface ISqlValidationService
{
    /// <summary>
    /// Validates that a SQL query is safe for read-only execution.
    /// </summary>
    /// <param name="query">The SQL query to validate</param>
    /// <returns>A tuple indicating if the query is valid and an error message if not</returns>
    SqlValidationResult ValidateQuery(string query);
}

/// <summary>
/// Result of SQL query validation
/// </summary>
public record SqlValidationResult(bool IsValid, string ErrorMessage);

/// <summary>
/// Implementation of SQL validation service that ensures only SELECT queries are allowed.
/// </summary>
public class SqlValidationService : ISqlValidationService
{
    /// <summary>
    /// Dangerous SQL keywords that should be rejected to prevent data modification.
    /// </summary>
    private static readonly string[] DangerousKeywords = new[]
    {
        "INSERT", "UPDATE", "DELETE", "MERGE", "DROP", "ALTER",
        "TRUNCATE", "EXEC", "EXECUTE", "CREATE", "GRANT", "REVOKE"
    };

    /// <summary>
    /// Pre-compiled regex patterns for dangerous keywords (word boundary matching).
    /// Using RegexOptions.Compiled for better performance in repeated calls.
    /// </summary>
    private static readonly Regex[] DangerousKeywordPatterns = DangerousKeywords
        .Select(keyword => new Regex($@"\b{keyword}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled))
        .ToArray();

    /// <summary>
    /// Validates that a SQL query is safe for read-only execution.
    /// Only SELECT queries are allowed; dangerous keywords are rejected.
    /// </summary>
    /// <param name="query">The SQL query to validate</param>
    /// <returns>A SqlValidationResult indicating if the query is valid</returns>
    public SqlValidationResult ValidateQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new SqlValidationResult(false, "Query cannot be empty.");
        }

        // Normalize the query for checking
        var normalizedQuery = query.Trim();

        // Check if query starts with SELECT (case-insensitive)
        if (!normalizedQuery.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            return new SqlValidationResult(false, "Only SELECT queries are allowed. Query must start with SELECT.");
        }

        // Check for dangerous keywords using pre-compiled patterns
        for (int i = 0; i < DangerousKeywordPatterns.Length; i++)
        {
            if (DangerousKeywordPatterns[i].IsMatch(normalizedQuery))
            {
                return new SqlValidationResult(false, $"Query contains forbidden keyword: {DangerousKeywords[i]}. Only read-only SELECT queries are allowed.");
            }
        }

        return new SqlValidationResult(true, string.Empty);
    }
}
