using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for Request operations
/// Abstracts business logic from presentation layer
/// </summary>
public interface IRequestService
{
    Task<Request?> GetRequestByIdAsync(int id);
    Task<IEnumerable<Request>> GetAllRequestsAsync();
    Task<IEnumerable<Request>> GetPendingRequestsAsync();
    Task<Request> CreateRequestAsync(string name, string email, string phone, string eventType, DateTime preferredDate, string location, string message);
    Task SetRequestDateRangeAsync(int id, DateTime endDate);
    Task UpdateRequestStatusAsync(int id, RequestStatus status);
    Task DeleteRequestAsync(int id);
}
