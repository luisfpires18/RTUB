namespace RTUB.Application.Services;

/// <summary>
/// Provides context for audit logging operations including the current user information.
/// This service is scoped per request and holds the authenticated user's details for audit trail.
/// </summary>
public class AuditContext
{
    private string? _userName;
    private string? _userId;

    /// <summary>
    /// Gets the username of the current user performing the operation.
    /// </summary>
    public string? UserName => _userName;
    
    /// <summary>
    /// Gets the user ID of the current user performing the operation.
    /// </summary>
    public string? UserId => _userId;

    /// <summary>
    /// Sets the current user information for audit logging.
    /// </summary>
    /// <param name="userName">The username of the authenticated user.</param>
    /// <param name="userId">The user ID of the authenticated user.</param>
    public void SetUser(string? userName, string? userId)
    {
        _userName = userName;
        _userId = userId;
    }

    /// <summary>
    /// Clears the current user information from the audit context.
    /// </summary>
    public void Clear()
    {
        _userName = null;
        _userId = null;
    }
}
