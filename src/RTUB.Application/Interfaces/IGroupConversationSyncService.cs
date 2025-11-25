namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for synchronizing default group conversations
/// </summary>
public interface IGroupConversationSyncService
{
    /// <summary>
    /// Synchronizes all default group conversations
    /// Creates groups if they don't exist and updates participants
    /// </summary>
    Task SyncDefaultGroupsAsync();
}
