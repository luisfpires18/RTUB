using RTUB.Application.Interfaces;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;
using RTUB.Core.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

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
    private readonly IDocumentStorageService _documentStorageService;
    private readonly IRepository<LogisticsCardAssignment> _assignmentRepository;
    private readonly IRepository<LogisticsCardReminder> _reminderRepository;

    public LogisticsCardService(
        ILogisticsCardRepository cardRepository, 
        IEventRepository eventRepository,
        IDocumentStorageService documentStorageService,
        IRepository<LogisticsCardAssignment> assignmentRepository,
        IRepository<LogisticsCardReminder> reminderRepository)
    {
        _cardRepository = cardRepository;
        _eventRepository = eventRepository;
        _documentStorageService = documentStorageService;
        _assignmentRepository = assignmentRepository;
        _reminderRepository = reminderRepository;
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

    public async Task AddCardAssignmentAsync(int cardId, string userId)
    {
        var card = await _cardRepository.GetByIdAsync(cardId);
        if (card == null)
            throw new InvalidOperationException($"Cartão com ID {cardId} não encontrado");

        // Check if assignment already exists
        var existingAssignment = await _assignmentRepository.Query()
            .FirstOrDefaultAsync(a => a.CardId == cardId && a.UserId == userId);

        if (existingAssignment != null)
            throw new InvalidOperationException($"Utilizador já está atribuído a este cartão");

        var assignment = LogisticsCardAssignment.Create(cardId, userId);
        await _assignmentRepository.AddAsync(assignment);
    }

    public async Task RemoveCardAssignmentAsync(int cardId, string userId)
    {
        var assignment = await _assignmentRepository.Query()
            .FirstOrDefaultAsync(a => a.CardId == cardId && a.UserId == userId);

        if (assignment == null)
            throw new InvalidOperationException($"Atribuição não encontrada");

        await _assignmentRepository.DeleteAsync(assignment);
    }

    public async Task<IEnumerable<LogisticsCardAssignment>> GetCardAssignmentsAsync(int cardId)
    {
        return await _assignmentRepository.Query()
            .Include(a => a.User)
            .Where(a => a.CardId == cardId)
            .ToListAsync();
    }

    public async Task<string> UploadCardAttachmentAsync(int cardId, string boardName, string fileName, Stream fileStream, string contentType, string environmentName)
    {
        var card = await _cardRepository.GetByIdAsync(cardId);
        if (card == null)
            throw new InvalidOperationException($"Cartão com ID {cardId} não encontrado");

        // Sanitize board name to prevent directory traversal attacks
        var sanitizedBoardName = SanitizePathComponent(boardName);
        
        // Get current fiscal year
        var fiscalYear = FiscalYearHelper.GetCurrentFiscalYearString();

        // Build folder path: docs/{EnvironmentName}/{FiscalYear}/Logistics/{BoardName}/
        var folderPath = $"docs/{environmentName}/{fiscalYear}/Logistics/{sanitizedBoardName}/";

        // Ensure folder exists
        await _documentStorageService.CreateFolderAsync(folderPath);

        // Upload the document
        return await _documentStorageService.UploadDocumentAsync(folderPath, fileName, fileStream, contentType);
    }

    public async Task<List<DocumentMetadata>> GetCardAttachmentsAsync(int cardId, string boardName, string environmentName)
    {
        var card = await _cardRepository.GetByIdAsync(cardId);
        if (card == null)
            throw new InvalidOperationException($"Cartão com ID {cardId} não encontrado");

        // Sanitize board name to prevent directory traversal attacks
        var sanitizedBoardName = SanitizePathComponent(boardName);
        
        // Get current fiscal year
        var fiscalYear = FiscalYearHelper.GetCurrentFiscalYearString();

        // Build folder path: docs/{EnvironmentName}/{FiscalYear}/Logistics/{BoardName}/
        var folderPath = $"docs/{environmentName}/{fiscalYear}/Logistics/{sanitizedBoardName}/";

        // Ensure folder exists before listing (prevents null reference when folder hasn't been created yet)
        await _documentStorageService.CreateFolderAsync(folderPath);

        // List all documents in the folder
        return await _documentStorageService.ListDocumentsInFolderAsync(folderPath);
    }

    public async Task DeleteCardAttachmentAsync(string documentPath)
    {
        await _documentStorageService.DeleteDocumentAsync(documentPath);
    }
    
    /// <summary>
    /// Regex pattern for allowed path characters: alphanumeric, hyphen, underscore, space, and common accented Portuguese characters
    /// </summary>
    private static readonly Regex SafePathRegex = new(@"[^a-zA-Z0-9\-_\s\u00C0-\u00FF]", RegexOptions.Compiled);
    
    /// <summary>
    /// Sanitizes a path component to prevent directory traversal attacks
    /// Uses regex-based approach for robust security
    /// </summary>
    private static string SanitizePathComponent(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;
        
        // First, replace directory traversal patterns
        var sanitized = input.Replace("..", "");
        
        // Then remove all characters that are not allowed using regex
        sanitized = SafePathRegex.Replace(sanitized, "");
        
        // Trim and ensure we have a valid result
        sanitized = sanitized.Trim();
        
        // If completely empty after sanitization, use a fallback
        if (string.IsNullOrEmpty(sanitized))
            return "unnamed";
        
        return sanitized;
    }

    public async Task<LogisticsCardReminder> CreateCardReminderAsync(int cardId, ReminderFrequency frequency, string targetUserIds, DateTime nextReminderDate)
    {
        var card = await _cardRepository.GetByIdAsync(cardId);
        if (card == null)
            throw new InvalidOperationException($"Cartão com ID {cardId} não encontrado");

        var reminder = LogisticsCardReminder.Create(cardId, frequency, targetUserIds, nextReminderDate);
        return await _reminderRepository.AddAsync(reminder);
    }

    public async Task<IEnumerable<LogisticsCardReminder>> GetCardRemindersAsync(int cardId)
    {
        return await _reminderRepository.Query()
            .Where(r => r.CardId == cardId && r.IsActive)
            .OrderBy(r => r.NextReminderDate)
            .ToListAsync();
    }

    public async Task DeactivateCardReminderAsync(int reminderId)
    {
        var reminder = await _reminderRepository.GetByIdAsync(reminderId);
        if (reminder == null)
            throw new InvalidOperationException($"Lembrete com ID {reminderId} não encontrado");

        reminder.Deactivate();
        await _reminderRepository.UpdateAsync(reminder);
    }
}
