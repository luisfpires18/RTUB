using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;
using RTUB.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace RTUB.Application.Services;

/// <summary>
/// Logistics Card service implementation
/// Contains business logic for logistics card operations
/// Refactored to use only repository pattern - no direct DbContext access
/// </summary>
public class LogisticsCardService : ILogisticsCardService
{
    private readonly ILogisticsCardRepository _cardRepository;
    private readonly IEventRepository _eventRepository;

    public LogisticsCardService(ILogisticsCardRepository cardRepository, IEventRepository eventRepository)
    {
        _cardRepository = cardRepository;
        _eventRepository = eventRepository;
    }

    public async Task<LogisticsCard?> GetCardByIdAsync(int id)
    {
        return await _cardRepository.Query()
            .Include(c => c.Event)
            .Include(c => c.AssignedToUser)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<IEnumerable<LogisticsCard>> GetCardsByListIdAsync(int listId)
    {
        return await _cardRepository.GetCardsByListIdAsync(listId);
    }

    public async Task<LogisticsCard> CreateCardAsync(string title, int listId, int position, string description = "")
    {
        var card = LogisticsCard.Create(title, listId, position, description);
        return await _cardRepository.AddAsync(card);
    }

    public async Task UpdateCardAsync(int id, string title, string description)
    {
        var card = await _cardRepository.GetByIdAsync(id);
        if (card == null)
            throw new InvalidOperationException($"Cartão com ID {id} não encontrado");

        card.UpdateContent(title, description);
        await _cardRepository.UpdateAsync(card);
    }

    public async Task MoveCardAsync(int id, int newListId, int newPosition)
    {
        var card = await _cardRepository.GetByIdAsync(id);
        if (card == null)
            throw new InvalidOperationException($"Cartão com ID {id} não encontrado");

        card.MoveToList(newListId, newPosition);
        await _cardRepository.UpdateAsync(card);
    }

    public async Task UpdateCardPositionAsync(int id, int position)
    {
        var card = await _cardRepository.GetByIdAsync(id);
        if (card == null)
            throw new InvalidOperationException($"Cartão com ID {id} não encontrado");

        card.UpdatePosition(position);
        await _cardRepository.UpdateAsync(card);
    }

    public async Task AssociateCardWithEventAsync(int id, int? eventId)
    {
        var card = await _cardRepository.GetByIdAsync(id);
        if (card == null)
            throw new InvalidOperationException($"Cartão com ID {id} não encontrado");

        // Validate that event exists if eventId is provided
        if (eventId.HasValue)
        {
            var eventExists = await _eventRepository.AnyAsync(e => e.Id == eventId.Value);
            if (!eventExists)
                throw new InvalidOperationException($"Evento com ID {eventId.Value} não encontrado");
        }

        card.AssociateWithEvent(eventId);
        await _cardRepository.UpdateAsync(card);
    }

    public async Task AssignCardToUserAsync(int id, string? userId)
    {
        var card = await _cardRepository.GetByIdAsync(id);
        if (card == null)
            throw new InvalidOperationException($"Cartão com ID {id} não encontrado");

        // Note: User validation would require IUserRepository which doesn't exist yet
        // For now, we trust the userId is valid (it will fail at DB constraint level if not)

        card.AssignToUser(userId);
        await _cardRepository.UpdateAsync(card);
    }

    public async Task DeleteCardAsync(int id)
    {
        var card = await _cardRepository.GetByIdAsync(id);
        if (card == null)
            throw new InvalidOperationException($"Cartão com ID {id} não encontrado");

        await _cardRepository.DeleteAsync(card);
    }

    public async Task SetCardStatusAsync(int id, CardStatus status)
    {
        var card = await _cardRepository.GetByIdAsync(id);
        if (card == null)
            throw new InvalidOperationException($"Cartão com ID {id} não encontrado");

        card.SetStatus(status);
        await _cardRepository.UpdateAsync(card);
    }

    public async Task SetCardLabelsAsync(int id, string? labels)
    {
        var card = await _cardRepository.GetByIdAsync(id);
        if (card == null)
            throw new InvalidOperationException($"Cartão com ID {id} não encontrado");

        card.SetLabels(labels);
        await _cardRepository.UpdateAsync(card);
    }

    public async Task SetCardDatesAsync(int id, DateTime? startDate, DateTime? dueDate, DateTime? reminderDate)
    {
        var card = await _cardRepository.GetByIdAsync(id);
        if (card == null)
            throw new InvalidOperationException($"Cartão com ID {id} não encontrado");

        card.SetDates(startDate, dueDate, reminderDate);
        await _cardRepository.UpdateAsync(card);
    }

    public async Task SetCardChecklistAsync(int id, string? checklistJson)
    {
        var card = await _cardRepository.GetByIdAsync(id);
        if (card == null)
            throw new InvalidOperationException($"Cartão com ID {id} não encontrado");

        card.SetChecklist(checklistJson);
        await _cardRepository.UpdateAsync(card);
    }

    public async Task SetCardAttachmentsAsync(int id, string? attachmentsJson)
    {
        var card = await _cardRepository.GetByIdAsync(id);
        if (card == null)
            throw new InvalidOperationException($"Cartão com ID {id} não encontrado");

        card.SetAttachments(attachmentsJson);
        await _cardRepository.UpdateAsync(card);
    }
}
