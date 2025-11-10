namespace RTUB.Application.Services;

/// <summary>
/// Service to notify components when a profile picture is updated
/// </summary>
public class ProfilePictureUpdateService
{
    private readonly List<Func<Task>> _subscribers = new();

    /// <summary>
    /// Subscribe to profile picture update notifications
    /// </summary>
    public void Subscribe(Func<Task> callback)
    {
        _subscribers.Add(callback);
    }

    /// <summary>
    /// Unsubscribe from profile picture update notifications
    /// </summary>
    public void Unsubscribe(Func<Task> callback)
    {
        _subscribers.Remove(callback);
    }

    /// <summary>
    /// Notify all subscribers that a profile picture has been updated
    /// </summary>
    public async Task NotifyProfilePictureUpdated()
    {
        var tasks = _subscribers.Select(callback => callback()).ToList();
        await Task.WhenAll(tasks);
    }
}
