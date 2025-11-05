namespace RTUB.Application.Services;

public class AuditContext
{
    private string? _userName;
    private string? _userId;

    public string? UserName => _userName;
    public string? UserId => _userId;

    public void SetUser(string? userName, string? userId)
    {
        _userName = userName;
        _userId = userId;
    }

    public void Clear()
    {
        _userName = null;
        _userId = null;
    }
}
