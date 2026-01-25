using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.DTOs;
using RTUB.Application.Extensions;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Exceptions;

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

    /// <summary>
    /// Initializes a new instance of the LogisticsCardService
    /// </summary>
    /// <param name="cardRepository">Repository for logistics card operations</param>
    /// <param name="eventRepository">Repository for event operations</param>
    /// <param name="documentStorageService">Service for document storage operations</param>
    /// <param name="assignmentRepository">Repository for card assignment operations</param>
    /// <param name="reminderRepository">Repository for card reminder operations</param>
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

    /// <summary>
    /// Gets a logistics card by its ID with event and assigned user information
    /// </summary>
    /// <param name="id">The ID of the card to retrieve</param>
    /// <returns>The logistics card if found, null otherwise</returns>
    public async Task<LogisticsCard?> GetCardByIdAsync(int id)
    {
        return await _cardRepository.Query()
            .Include(c => c.Event)
            .Include(c => c.AssignedToUser)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    /// <summary>
    /// Gets all cards for a specific list
    /// </summary>
    /// <param name="listId">The ID of the list</param>
    /// <returns>Collection of cards for the specified list</returns>
    public async Task<IEnumerable<LogisticsCard>> GetCardsByListIdAsync(int listId)
    {
        return await _cardRepository.GetCardsByListIdAsync(listId);
    }

    /// <summary>
    /// Creates a new logistics card
    /// </summary>
    /// <param name="title">The title of the card</param>
    /// <param name="listId">The ID of the list this card belongs to</param>
    /// <param name="position">The position of the card within the list</param>
    /// <param name="description">Optional description of the card</param>
    /// <returns>The created logistics card</returns>
    public async Task<LogisticsCard> CreateCardAsync(string title, int listId, int position, string description = "")
    {
        var card = LogisticsCard.Create(title, listId, position, description);
        return await _cardRepository.AddAsync(card);
    }

    /// <summary>
    /// Updates the title and description of a logistics card
    /// </summary>
    /// <param name="id">The ID of the card to update</param>
    /// <param name="title">The new title for the card</param>
    /// <param name="description">The new description for the card</param>
    /// <exception cref="EntityNotFoundException">Thrown when the card is not found</exception>
    public async Task UpdateCardAsync(int id, string title, string description)
    {
        var card = await _cardRepository.GetByIdOrThrowAsync(id);

        card.UpdateContent(title, description);
        await _cardRepository.UpdateAsync(card);
    }

    /// <summary>
    /// Moves a card to a different list and position
    /// </summary>
    /// <param name="id">The ID of the card to move</param>
    /// <param name="newListId">The ID of the target list</param>
    /// <param name="newPosition">The new position within the target list</param>
    /// <exception cref="EntityNotFoundException">Thrown when the card is not found</exception>
    public async Task MoveCardAsync(int id, int newListId, int newPosition)
    {
        var card = await _cardRepository.GetByIdOrThrowAsync(id);

        card.MoveToList(newListId, newPosition);
        await _cardRepository.UpdateAsync(card);
    }

    /// <summary>
    /// Updates the position of a card within its current list
    /// </summary>
    /// <param name="id">The ID of the card to update</param>
    /// <param name="position">The new position for the card</param>
    /// <exception cref="EntityNotFoundException">Thrown when the card is not found</exception>
    public async Task UpdateCardPositionAsync(int id, int position)
    {
        var card = await _cardRepository.GetByIdOrThrowAsync(id);

        card.UpdatePosition(position);
        await _cardRepository.UpdateAsync(card);
    }

    /// <summary>
    /// Associates a logistics card with an event
    /// </summary>
    /// <param name="id">The ID of the card</param>
    /// <param name="eventId">The ID of the event to associate (null to remove association)</param>
    /// <exception cref="EntityNotFoundException">Thrown when the card is not found</exception>
    /// <exception cref="InvalidOperationException">Thrown when the event is not found</exception>
    public async Task AssociateCardWithEventAsync(int id, int? eventId)
    {
        var card = await _cardRepository.GetByIdOrThrowAsync(id);

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

    /// <summary>
    /// Assigns a logistics card to a user
    /// </summary>
    /// <param name="id">The ID of the card</param>
    /// <param name="userId">The ID of the user to assign (null to remove assignment)</param>
    /// <exception cref="EntityNotFoundException">Thrown when the card is not found</exception>
    public async Task AssignCardToUserAsync(int id, string? userId)
    {
        var card = await _cardRepository.GetByIdOrThrowAsync(id);

        // Note: User validation would require IUserRepository which doesn't exist yet
        // For now, we trust the userId is valid (it will fail at DB constraint level if not)

        card.AssignToUser(userId);
        await _cardRepository.UpdateAsync(card);
    }

    /// <summary>
    /// Deletes a logistics card
    /// </summary>
    /// <param name="id">The ID of the card to delete</param>
    /// <exception cref="EntityNotFoundException">Thrown when the card is not found</exception>
    public async Task DeleteCardAsync(int id)
    {
        var card = await _cardRepository.GetByIdOrThrowAsync(id);

        await _cardRepository.DeleteAsync(card);
    }

    /// <summary>
    /// Sets the status of a logistics card
    /// </summary>
    /// <param name="id">The ID of the card</param>
    /// <param name="status">The new status for the card</param>
    /// <exception cref="EntityNotFoundException">Thrown when the card is not found</exception>
    public async Task SetCardStatusAsync(int id, CardStatus status)
    {
        var card = await _cardRepository.GetByIdOrThrowAsync(id);

        card.SetStatus(status);
        await _cardRepository.UpdateAsync(card);
    }

    /// <summary>
    /// Sets the labels for a logistics card
    /// </summary>
    /// <param name="id">The ID of the card</param>
    /// <param name="labels">The labels to set (can be null)</param>
    /// <exception cref="EntityNotFoundException">Thrown when the card is not found</exception>
    public async Task SetCardLabelsAsync(int id, string? labels)
    {
        var card = await _cardRepository.GetByIdOrThrowAsync(id);

        card.SetLabels(labels);
        await _cardRepository.UpdateAsync(card);
    }

    /// <summary>
    /// Sets the dates for a logistics card
    /// </summary>
    /// <param name="id">The ID of the card</param>
    /// <param name="startDate">The start date (can be null)</param>
    /// <param name="dueDate">The due date (can be null)</param>
    /// <param name="reminderDate">The reminder date (can be null)</param>
    /// <exception cref="EntityNotFoundException">Thrown when the card is not found</exception>
    public async Task SetCardDatesAsync(int id, DateTime? startDate, DateTime? dueDate, DateTime? reminderDate)
    {
        var card = await _cardRepository.GetByIdOrThrowAsync(id);

        card.SetDates(startDate, dueDate, reminderDate);
        await _cardRepository.UpdateAsync(card);
    }

    /// <summary>
    /// Sets the checklist for a logistics card
    /// </summary>
    /// <param name="id">The ID of the card</param>
    /// <param name="checklistJson">The checklist JSON (can be null)</param>
    /// <exception cref="EntityNotFoundException">Thrown when the card is not found</exception>
    public async Task SetCardChecklistAsync(int id, string? checklistJson)
    {
        var card = await _cardRepository.GetByIdOrThrowAsync(id);

        card.SetChecklist(checklistJson);
        await _cardRepository.UpdateAsync(card);
    }

    /// <summary>
    /// Sets the attachments for a logistics card
    /// </summary>
    /// <param name="id">The ID of the card</param>
    /// <param name="attachmentsJson">The attachments JSON (can be null)</param>
    /// <exception cref="EntityNotFoundException">Thrown when the card is not found</exception>
    public async Task SetCardAttachmentsAsync(int id, string? attachmentsJson)
    {
        var card = await _cardRepository.GetByIdOrThrowAsync(id);

        card.SetAttachments(attachmentsJson);
        await _cardRepository.UpdateAsync(card);
    }

    /// <summary>
    /// Adds a user assignment to a logistics card
    /// </summary>
    /// <param name="cardId">The ID of the card</param>
    /// <param name="userId">The ID of the user to assign</param>
    /// <exception cref="EntityNotFoundException">Thrown when the card is not found</exception>
    /// <exception cref="InvalidOperationException">Thrown when the user is already assigned to the card</exception>
    public async Task AddCardAssignmentAsync(int cardId, string userId)
    {
        var card = await _cardRepository.GetByIdOrThrowAsync(cardId);

        // Check if assignment already exists
        var existingAssignment = await _assignmentRepository.Query()
            .FirstOrDefaultAsync(a => a.CardId == cardId && a.UserId == userId);

        if (existingAssignment != null)
            throw new InvalidOperationException($"Utilizador já está atribuído a este cartão");

        var assignment = LogisticsCardAssignment.Create(cardId, userId);
        await _assignmentRepository.AddAsync(assignment);
    }

    /// <summary>
    /// Removes a user assignment from a logistics card
    /// </summary>
    /// <param name="cardId">The ID of the card</param>
    /// <param name="userId">The ID of the user to remove assignment for</param>
    /// <exception cref="InvalidOperationException">Thrown when the assignment is not found</exception>
    public async Task RemoveCardAssignmentAsync(int cardId, string userId)
    {
        var assignment = await _assignmentRepository.Query()
            .FirstOrDefaultAsync(a => a.CardId == cardId && a.UserId == userId);

        if (assignment == null)
            throw new InvalidOperationException($"Atribuição não encontrada");

        await _assignmentRepository.DeleteAsync(assignment);
    }

    /// <summary>
    /// Gets all user assignments for a logistics card
    /// </summary>
    /// <param name="cardId">The ID of the card</param>
    /// <returns>Collection of card assignments with user information</returns>
    public async Task<IEnumerable<LogisticsCardAssignment>> GetCardAssignmentsAsync(int cardId)
    {
        return await _assignmentRepository.Query()
            .Include(a => a.User)
            .Where(a => a.CardId == cardId)
            .ToListAsync();
    }

    /// <summary>
    /// Uploads an attachment document for a logistics card
    /// </summary>
    /// <param name="cardId">The ID of the card</param>
    /// <param name="boardName">The name of the board (sanitized for path)</param>
    /// <param name="fileName">The name of the file to upload</param>
    /// <param name="fileStream">The file stream</param>
    /// <param name="contentType">The content type of the file</param>
    /// <param name="environmentName">The environment name for folder organization</param>
    /// <returns>The path to the uploaded document</returns>
    /// <exception cref="EntityNotFoundException">Thrown when the card is not found</exception>
    public async Task<string> UploadCardAttachmentAsync(int cardId, string boardName, string fileName, Stream fileStream, string contentType, string environmentName)
    {
        var card = await _cardRepository.GetByIdOrThrowAsync(cardId);

        // Sanitize board name to prevent directory traversal attacks
        var sanitizedBoardName = SanitizePathComponent(boardName);

        // Get current fiscal year
        var fiscalYear = FiscalYearHelper.GetCurrentFiscalYearString();

        // Build folder path: docs/{EnvironmentName}/{FiscalYear}/Logistics/{BoardName}/
        var folderPath = $"docs/{environmentName}/{fiscalYear}/Logistics/{sanitizedBoardName}/";

        // Upload the document (folder will be created automatically if it doesn't exist)
        return await _documentStorageService.UploadDocumentAsync(folderPath, fileName, fileStream, contentType);
    }

    /// <summary>
    /// Gets all attachment documents for a logistics card
    /// </summary>
    /// <param name="cardId">The ID of the card</param>
    /// <param name="boardName">The name of the board (sanitized for path)</param>
    /// <param name="environmentName">The environment name for folder organization</param>
    /// <returns>List of document metadata for attachments</returns>
    /// <exception cref="EntityNotFoundException">Thrown when the card is not found</exception>
    public async Task<List<DocumentMetadata>> GetCardAttachmentsAsync(int cardId, string boardName, string environmentName)
    {
        var card = await _cardRepository.GetByIdOrThrowAsync(cardId);

        // Sanitize board name to prevent directory traversal attacks
        var sanitizedBoardName = SanitizePathComponent(boardName);

        // Get current fiscal year
        var fiscalYear = FiscalYearHelper.GetCurrentFiscalYearString();

        // Build folder path: docs/{EnvironmentName}/{FiscalYear}/Logistics/{BoardName}/
        var folderPath = $"docs/{environmentName}/{fiscalYear}/Logistics/{sanitizedBoardName}/";

        // List all documents in the folder
        // Note: ListDocumentsInFolderAsync handles non-existent folders gracefully by returning an empty list
        // Folders are only created when files are actually uploaded via UploadCardAttachmentAsync
        return await _documentStorageService.ListDocumentsInFolderAsync(folderPath);
    }

    /// <summary>
    /// Deletes an attachment document
    /// </summary>
    /// <param name="documentPath">The path to the document to delete</param>
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

    /// <summary>
    /// Creates a reminder for a logistics card
    /// </summary>
    /// <param name="cardId">The ID of the card</param>
    /// <param name="frequency">The reminder frequency</param>
    /// <param name="targetUserIds">Comma-separated user IDs to send reminders to</param>
    /// <param name="nextReminderDate">The date for the next reminder</param>
    /// <returns>The created reminder</returns>
    /// <exception cref="EntityNotFoundException">Thrown when the card is not found</exception>
    public async Task<LogisticsCardReminder> CreateCardReminderAsync(int cardId, ReminderFrequency frequency, string targetUserIds, DateTime nextReminderDate)
    {
        var card = await _cardRepository.GetByIdOrThrowAsync(cardId);

        var reminder = LogisticsCardReminder.Create(cardId, frequency, targetUserIds, nextReminderDate);
        return await _reminderRepository.AddAsync(reminder);
    }

    /// <summary>
    /// Gets all active reminders for a logistics card
    /// </summary>
    /// <param name="cardId">The ID of the card</param>
    /// <returns>Collection of active reminders ordered by next reminder date</returns>
    public async Task<IEnumerable<LogisticsCardReminder>> GetCardRemindersAsync(int cardId)
    {
        return await _reminderRepository.Query()
            .Where(r => r.CardId == cardId && r.IsActive)
            .OrderBy(r => r.NextReminderDate)
            .ToListAsync();
    }

    /// <summary>
    /// Deactivates a card reminder
    /// </summary>
    /// <param name="reminderId">The ID of the reminder to deactivate</param>
    /// <exception cref="EntityNotFoundException">Thrown when the reminder is not found</exception>
    public async Task DeactivateCardReminderAsync(int reminderId)
    {
        var reminder = await _reminderRepository.GetByIdOrThrowAsync(reminderId);

        reminder.Deactivate();
        await _reminderRepository.UpdateAsync(reminder);
    }
}
