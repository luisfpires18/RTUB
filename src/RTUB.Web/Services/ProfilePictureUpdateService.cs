namespace RTUB.Web.Services;

/// <summary>
/// Service to notify components when profile picture is updated
/// </summary>
public class ProfilePictureUpdateService
{
    public event Action? OnProfilePictureUpdated;

    public void NotifyProfilePictureUpdated()
    {
        Console.WriteLine($"[ProfilePictureUpdateService] NotifyProfilePictureUpdated called at {DateTime.Now:HH:mm:ss.fff}");
        Console.WriteLine($"[ProfilePictureUpdateService] Subscribers count: {OnProfilePictureUpdated?.GetInvocationList().Length ?? 0}");
        OnProfilePictureUpdated?.Invoke();
    }
}
