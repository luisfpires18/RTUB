using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for Logistics Card operations
/// Provides methods to manage cards in the logistics Kanban board system
/// </summary>
public interface ILogisticsCardService
{
    /// <summary>
    /// Retrieves a logistics card by its unique identifier
    /// </summary>
    /// <param name="id">The unique identifier of the card</param>
    /// <returns>The logistics card if found; otherwise, null</returns>
    Task<LogisticsCard?> GetCardByIdAsync(int id);
    
    /// <summary>
    /// Retrieves all cards belonging to a specific list
    /// </summary>
    /// <param name="listId">The unique identifier of the list</param>
    /// <returns>A collection of logistics cards ordered by position</returns>
    Task<IEnumerable<LogisticsCard>> GetCardsByListIdAsync(int listId);
    
    /// <summary>
    /// Creates a new logistics card
    /// </summary>
    /// <param name="title">The title of the card</param>
    /// <param name="listId">The unique identifier of the list to add the card to</param>
    /// <param name="position">The position of the card in the list</param>
    /// <param name="description">Optional description of the card</param>
    /// <returns>The created logistics card</returns>
    Task<LogisticsCard> CreateCardAsync(string title, int listId, int position, string description = "");
    
    /// <summary>
    /// Updates the title and description of a card
    /// </summary>
    /// <param name="id">The unique identifier of the card</param>
    /// <param name="title">The new title</param>
    /// <param name="description">The new description</param>
    /// <exception cref="InvalidOperationException">Thrown when the card is not found</exception>
    Task UpdateCardAsync(int id, string title, string description);
    
    /// <summary>
    /// Moves a card to a different list and position
    /// </summary>
    /// <param name="id">The unique identifier of the card</param>
    /// <param name="newListId">The unique identifier of the destination list</param>
    /// <param name="newPosition">The new position in the destination list</param>
    /// <exception cref="InvalidOperationException">Thrown when the card is not found</exception>
    Task MoveCardAsync(int id, int newListId, int newPosition);
    
    /// <summary>
    /// Updates the position of a card within its current list
    /// </summary>
    /// <param name="id">The unique identifier of the card</param>
    /// <param name="position">The new position</param>
    /// <exception cref="InvalidOperationException">Thrown when the card is not found</exception>
    Task UpdateCardPositionAsync(int id, int position);
    
    /// <summary>
    /// Associates a card with an event
    /// </summary>
    /// <param name="id">The unique identifier of the card</param>
    /// <param name="eventId">The unique identifier of the event to associate, or null to remove association</param>
    /// <exception cref="InvalidOperationException">Thrown when the card or event is not found</exception>
    Task AssociateCardWithEventAsync(int id, int? eventId);
    
    /// <summary>
    /// Assigns a card to a user
    /// </summary>
    /// <param name="id">The unique identifier of the card</param>
    /// <param name="userId">The unique identifier of the user to assign, or null to unassign</param>
    /// <exception cref="InvalidOperationException">Thrown when the card or user is not found</exception>
    Task AssignCardToUserAsync(int id, string? userId);
    
    /// <summary>
    /// Sets the status of a card
    /// </summary>
    /// <param name="id">The unique identifier of the card</param>
    /// <param name="status">The new status</param>
    /// <exception cref="InvalidOperationException">Thrown when the card is not found</exception>
    Task SetCardStatusAsync(int id, CardStatus status);
    
    /// <summary>
    /// Sets the labels of a card
    /// </summary>
    /// <param name="id">The unique identifier of the card</param>
    /// <param name="labels">Comma-separated string of labels, or null to clear labels</param>
    /// <exception cref="InvalidOperationException">Thrown when the card is not found</exception>
    Task SetCardLabelsAsync(int id, string? labels);
    
    /// <summary>
    /// Sets the date fields of a card
    /// </summary>
    /// <param name="id">The unique identifier of the card</param>
    /// <param name="startDate">Optional start date</param>
    /// <param name="dueDate">Optional due date</param>
    /// <param name="reminderDate">Optional reminder date</param>
    /// <exception cref="InvalidOperationException">Thrown when the card is not found</exception>
    Task SetCardDatesAsync(int id, DateTime? startDate, DateTime? dueDate, DateTime? reminderDate);
    
    /// <summary>
    /// Sets the checklist of a card
    /// </summary>
    /// <param name="id">The unique identifier of the card</param>
    /// <param name="checklistJson">JSON string representing the checklist, or null to clear</param>
    /// <exception cref="InvalidOperationException">Thrown when the card is not found</exception>
    Task SetCardChecklistAsync(int id, string? checklistJson);
    
    /// <summary>
    /// Sets the attachments of a card
    /// </summary>
    /// <param name="id">The unique identifier of the card</param>
    /// <param name="attachmentsJson">JSON string representing the attachments, or null to clear</param>
    /// <exception cref="InvalidOperationException">Thrown when the card is not found</exception>
    Task SetCardAttachmentsAsync(int id, string? attachmentsJson);
    
    /// <summary>
    /// Deletes a logistics card
    /// </summary>
    /// <param name="id">The unique identifier of the card to delete</param>
    /// <exception cref="InvalidOperationException">Thrown when the card is not found</exception>
    Task DeleteCardAsync(int id);
}
