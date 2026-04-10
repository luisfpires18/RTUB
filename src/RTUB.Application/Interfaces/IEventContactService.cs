using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for managing event contact tracking — records whether members have been contacted about an event.
/// </summary>
public interface IEventContactService
{
    /// <summary>
    /// Gets all contacts for a given event.
    /// </summary>
    Task<IEnumerable<EventContact>> GetContactsByEventIdAsync(int eventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific contact by event and user.
    /// </summary>
    Task<EventContact?> GetContactAsync(int eventId, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a member as contacted for an event, recording attendance decision and notes.
    /// Creates the EventContact record if it doesn't exist.
    /// </summary>
    Task<EventContact> MarkAsContactedAsync(int eventId, string userId, bool? willAttend, string? notes, string contactedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets a contact record back to not-contacted state.
    /// </summary>
    Task ResetContactAsync(int eventId, string userId, CancellationToken cancellationToken = default);
}
