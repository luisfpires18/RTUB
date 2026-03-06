namespace RTUB.Application.Interfaces;

/// <summary>
/// Result of SQL query validation.
/// </summary>
public record SqlValidationResult(bool IsValid, string ErrorMessage);

/// <summary>
/// Validates SQL queries to ensure they are safe for execution.
/// </summary>
public interface ISqlValidationService
{
    /// <summary>
    /// Validates that a SQL query is safe for read-only SELECT execution.
    /// </summary>
    SqlValidationResult ValidateQuery(string query);

    /// <summary>
    /// Validates that a SQL query is a safe data-modification query (INSERT, UPDATE, DELETE only).
    /// Blocks DDL (DROP, ALTER, TRUNCATE, CREATE) and other dangerous operations.
    /// </summary>
    SqlValidationResult ValidateModifyQuery(string query);
}
