using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;
using RTUB.Core.Enums;


namespace RTUB.Application.Services;

/// <summary>
/// Request service implementation using Repository pattern
/// Contains business logic for request operations
/// Follows Single Responsibility and Dependency Inversion principles
/// Now depends on IRequestRepository abstraction instead of concrete DbContext
/// </summary>
public class RequestService : IRequestService
{
    private readonly IRequestRepository _requestRepository;
    private readonly IEmailNotificationService _emailNotificationService;

    public RequestService(IRequestRepository requestRepository, IEmailNotificationService emailNotificationService)
    {
        _requestRepository = requestRepository;
        _emailNotificationService = emailNotificationService;
    }

    public async Task<Request?> GetRequestByIdAsync(int id)
    {
        return await _requestRepository.GetByIdAsync(id);
    }

    public async Task<IEnumerable<Request>> GetAllRequestsAsync()
    {
        return await _requestRepository.GetAllAsync();
    }

    public async Task<IEnumerable<Request>> GetPendingRequestsAsync()
    {
        return await _requestRepository.GetByStatusAsync(RequestStatus.Pending);
    }

    public async Task<Request> CreateRequestAsync(string name, string email, string phone, string eventType, DateTime preferredDate, string location, string message)
    {
        var request = Request.Create(name, email, phone, eventType, preferredDate, location, message);
        var createdRequest = await _requestRepository.AddAsync(request);

        // Send notification for new request
        await _emailNotificationService.SendNewRequestNotificationAsync(createdRequest.Id, name, email, eventType);

        return createdRequest;
    }

    public async Task SetRequestDateRangeAsync(int id, DateTime endDate)
    {
        var request = await _requestRepository.GetByIdAsync(id);
        if (request == null)
            throw new EntityNotFoundException(nameof(Request), id);

        request.SetDateRange(endDate);
        await _requestRepository.UpdateAsync(request);
    }

    public async Task UpdateRequestStatusAsync(int id, RequestStatus status)
    {
        var request = await _requestRepository.GetByIdAsync(id);
        if (request == null)
            throw new EntityNotFoundException(nameof(Request), id);

        var oldStatus = request.Status;
        request.UpdateStatus(status);
        await _requestRepository.UpdateAsync(request);

        // Send notification when status changes
        if (oldStatus != status)
        {
            await _emailNotificationService.SendRequestStatusChangedAsync(
                request.Id, request.Name, request.Email, oldStatus, status);
        }
    }

    public async Task DeleteRequestAsync(int id)
    {
        var request = await _requestRepository.GetByIdAsync(id);
        if (request == null)
            throw new EntityNotFoundException(nameof(Request), id);

        await _requestRepository.DeleteAsync(request);
    }
}
