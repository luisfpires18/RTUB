using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

public interface ITransportationService
{
    Task<Transportation?> GetByPostIdAsync(int postId);
    Task<Transportation> CreateForPostAsync(int postId, string vehicleDescription, int totalSeats, string? notes);
    Task UpdateAsync(int id, string vehicleDescription, int totalSeats, string? notes);
    Task DeleteByPostIdAsync(int postId);
    Task AddPassengerAsync(int transportationId, string userId);
    Task RemovePassengerAsync(int transportationId, string userId);
    Task<IEnumerable<ApplicationUser>> GetAllMembersAsync();
}
