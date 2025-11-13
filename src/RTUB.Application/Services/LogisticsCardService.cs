using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;

namespace RTUB.Application.Services;

/// <summary>
/// Logistics Card service implementation
/// Contains business logic for logistics card operations
/// </summary>
public class LogisticsCardService : ILogisticsCardService
{
    private readonly ApplicationDbContext _context;

    public LogisticsCardService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<LogisticsCard?> GetCardByIdAsync(int id)
    {
        return await _context.LogisticsCards
            .Include(c => c.Event)
            .Include(c => c.AssignedToUser)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<IEnumerable<LogisticsCard>> GetCardsByListIdAsync(int listId)
    {
        return await _context.LogisticsCards
            .Where(c => c.ListId == listId)
            .Include(c => c.Event)
            .Include(c => c.AssignedToUser)
            .OrderBy(c => c.Position)
            .ToListAsync();
    }

    public async Task<LogisticsCard> CreateCardAsync(string title, int listId, int position, string description = "")
    {
        var card = LogisticsCard.Create(title, listId, position, description);
        _context.LogisticsCards.Add(card);
        await _context.SaveChangesAsync();
        return card;
    }

    public async Task UpdateCardAsync(int id, string title, string description)
    {
        var card = await _context.LogisticsCards.FindAsync(id);
        if (card == null)
            throw new InvalidOperationException($"Cartão com ID {id} não encontrado");

        card.UpdateContent(title, description);
        await _context.SaveChangesAsync();
    }

    public async Task MoveCardAsync(int id, int newListId, int newPosition)
    {
        var card = await _context.LogisticsCards.FindAsync(id);
        if (card == null)
            throw new InvalidOperationException($"Cartão com ID {id} não encontrado");

        card.MoveToList(newListId, newPosition);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateCardPositionAsync(int id, int position)
    {
        var card = await _context.LogisticsCards.FindAsync(id);
        if (card == null)
            throw new InvalidOperationException($"Cartão com ID {id} não encontrado");

        card.UpdatePosition(position);
        await _context.SaveChangesAsync();
    }

    public async Task AssociateCardWithEventAsync(int id, int? eventId)
    {
        var card = await _context.LogisticsCards.FindAsync(id);
        if (card == null)
            throw new InvalidOperationException($"Cartão com ID {id} não encontrado");

        card.AssociateWithEvent(eventId);
        await _context.SaveChangesAsync();
    }

    public async Task AssignCardToUserAsync(int id, string? userId)
    {
        var card = await _context.LogisticsCards.FindAsync(id);
        if (card == null)
            throw new InvalidOperationException($"Cartão com ID {id} não encontrado");

        card.AssignToUser(userId);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteCardAsync(int id)
    {
        var card = await _context.LogisticsCards.FindAsync(id);
        if (card == null)
            throw new InvalidOperationException($"Cartão com ID {id} não encontrado");

        _context.LogisticsCards.Remove(card);
        await _context.SaveChangesAsync();
    }

    public async Task SetCardStatusAsync(int id, CardStatus status)
    {
        var card = await _context.LogisticsCards.FindAsync(id);
        if (card == null)
            throw new InvalidOperationException($"Cartão com ID {id} não encontrado");

        card.SetStatus(status);
        await _context.SaveChangesAsync();
    }

    public async Task SetCardLabelsAsync(int id, string? labels)
    {
        var card = await _context.LogisticsCards.FindAsync(id);
        if (card == null)
            throw new InvalidOperationException($"Cartão com ID {id} não encontrado");

        card.SetLabels(labels);
        await _context.SaveChangesAsync();
    }

    public async Task SetCardDatesAsync(int id, DateTime? startDate, DateTime? dueDate, DateTime? reminderDate)
    {
        var card = await _context.LogisticsCards.FindAsync(id);
        if (card == null)
            throw new InvalidOperationException($"Cartão com ID {id} não encontrado");

        card.SetDates(startDate, dueDate, reminderDate);
        await _context.SaveChangesAsync();
    }

    public async Task SetCardChecklistAsync(int id, string? checklistJson)
    {
        var card = await _context.LogisticsCards.FindAsync(id);
        if (card == null)
            throw new InvalidOperationException($"Cartão com ID {id} não encontrado");

        card.SetChecklist(checklistJson);
        await _context.SaveChangesAsync();
    }

    public async Task SetCardAttachmentsAsync(int id, string? attachmentsJson)
    {
        var card = await _context.LogisticsCards.FindAsync(id);
        if (card == null)
            throw new InvalidOperationException($"Cartão com ID {id} não encontrado");

        card.SetAttachments(attachmentsJson);
        await _context.SaveChangesAsync();
    }
}
