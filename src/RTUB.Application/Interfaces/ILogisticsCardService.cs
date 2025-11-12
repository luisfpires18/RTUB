using RTUB.Core.Entities;

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
    Task DeleteCardAsync(int id);
}
