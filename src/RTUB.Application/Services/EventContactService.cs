using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing event contact tracking.
/// </summary>
public class EventContactService : IEventContactService
{
    private readonly IRepository<EventContact> _repository;
    private readonly ILogger<EventContactService> _logger;

    public EventContactService(
        IRepository<EventContact> repository,
        ILogger<EventContactService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<EventContact>> GetContactsByEventIdAsync(int eventId, CancellationToken cancellationToken = default)
    {
        return await _repository.QueryAsync(
            q => q.Where(c => c.EventId == eventId)
                  .Include(c => c.User)
                  .ToListAsync(cancellationToken),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<EventContact?> GetContactAsync(int eventId, string userId, CancellationToken cancellationToken = default)
    {
        return await _repository.QueryAsync(
            q => q.Where(c => c.EventId == eventId && c.UserId == userId)
                  .Include(c => c.User)
                  .FirstOrDefaultAsync(cancellationToken),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<EventContact> MarkAsContactedAsync(int eventId, string userId, bool? willAttend, string? notes, string contactedBy, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(contactedBy);

        var existing = await _repository.FirstOrDefaultAsync(c => c.EventId == eventId && c.UserId == userId);

        if (existing != null)
        {
            existing.MarkAsContacted(willAttend, notes, contactedBy);
            await _repository.UpdateAsync(existing);
            _logger.LogInformation("Updated contact for event {EventId}, user {UserId}", eventId, userId);
            return existing;
        }

        var contact = EventContact.Create(eventId, userId);
        contact.MarkAsContacted(willAttend, notes, contactedBy);
        await _repository.AddAsync(contact);
        _logger.LogInformation("Created contact for event {EventId}, user {UserId}", eventId, userId);
        return contact;
    }

    /// <inheritdoc />
    public async Task ResetContactAsync(int eventId, string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var existing = await _repository.FirstOrDefaultAsync(c => c.EventId == eventId && c.UserId == userId);

        if (existing == null)
            throw new EntityNotFoundException(nameof(EventContact), $"EventId={eventId}, UserId={userId}");

        existing.ResetContact();
        await _repository.UpdateAsync(existing);
        _logger.LogInformation("Reset contact for event {EventId}, user {UserId}", eventId, userId);
    }
}
