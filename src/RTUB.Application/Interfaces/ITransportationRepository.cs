using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

public interface ITransportationRepository : IRepository<Transportation>
{
    Task<Transportation?> GetByPostIdAsync(int postId);
    Task AddPassengerAsync(TransportationPassenger passenger);
    Task RemovePassengerAsync(int transportationId, string passengerId);
}
