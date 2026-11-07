namespace RTUB.Core.Entities;

/// <summary>
/// Task 082 (issue #263, docs/push-notifications.md → Preference keys): one row = one push kind the member turned
/// <b>off</b>. Absence means on, so every key (a future one included) starts on and the rollout stores nothing. Per
/// account, not per device: it follows the member across devices, while the subscription stays per device. Only the
/// device push is muted; the "Notificações RTUB" copy in /messages is still kept. The mandatory key
/// (<c>pending-actions.daily</c>) never has a row; the service refuses it.
/// </summary>
public class PushPreference
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    /// <summary>A stable key from the catalogue, e.g. "events.new"; never a translated title.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>UTC.</summary>
    public DateTime MutedAt { get; set; }
}
