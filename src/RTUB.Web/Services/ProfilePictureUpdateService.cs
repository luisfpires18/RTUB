namespace RTUB.Web.Services;

/// <summary>
/// Service to notify components when profile picture is updated
/// </summary>
public class ProfilePictureUpdateService
{
    public event Action? OnProfilePictureUpdated;

    public void NotifyProfilePictureUpdated()
    {
        OnProfilePictureUpdated?.Invoke();
    }
}
