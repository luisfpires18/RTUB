namespace RTUB.Application.DTOs;

/// <summary>
/// DTO for transaction history entries retrieved from audit logs
/// </summary>
public class TransactionHistoryEntryDto
{
    /// <summary>
    /// The audit log ID
    /// </summary>
    public int AuditLogId { get; set; }

    /// <summary>
    /// The transaction ID
    /// </summary>
    public int TransactionId { get; set; }

    /// <summary>
    /// The timestamp of the action
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// The activity name that the transaction belongs to
    /// </summary>
    public string ActivityName { get; set; } = string.Empty;

    /// <summary>
    /// The transaction description
    /// </summary>
    public string TransactionDescription { get; set; } = string.Empty;

    /// <summary>
    /// The username who performed the action
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// The action performed (Created, Modified, Deleted)
    /// </summary>
    public string Action { get; set; } = string.Empty;
}
