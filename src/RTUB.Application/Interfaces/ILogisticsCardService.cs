using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for Logistics Card operations
/// </summary>
public interface ILogisticsCardService
{
    Task<LogisticsCard?> GetCardByIdAsync(int id);
    Task<IEnumerable<LogisticsCard>> GetCardsByListIdAsync(int listId);
    Task<LogisticsCard> CreateCardAsync(string title, int listId, int position, string description = "");
    Task UpdateCardAsync(int id, string title, string description);
    Task MoveCardAsync(int id, int newListId, int newPosition);
    Task UpdateCardPositionAsync(int id, int position);
    Task AssociateCardWithEventAsync(int id, int? eventId);
    Task AssignCardToUserAsync(int id, string? userId);
    Task SetCardStatusAsync(int id, CardStatus status);
    Task SetCardLabelsAsync(int id, string? labels);
    Task SetCardDatesAsync(int id, DateTime? startDate, DateTime? dueDate, DateTime? reminderDate);
    Task SetCardChecklistAsync(int id, string? checklistJson);
    Task SetCardAttachmentsAsync(int id, string? attachmentsJson);
    Task DeleteCardAsync(int id);
}
