using RTUB.Application.Interfaces;
using RTUB.Application.Data;
using RTUB.Application.Extensions;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;
using RTUB.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace RTUB.Application.Services;

/// <summary>
/// Meeting request service implementation
/// Contains business logic for CV meeting request operations
/// </summary>
public class MeetingRequestService : IMeetingRequestService
{
    private readonly IMeetingRequestRepository _meetingRequestRepository;

    public MeetingRequestService(IMeetingRequestRepository meetingRequestRepository)
    {
        _meetingRequestRepository = meetingRequestRepository;
    }

    public async Task<IEnumerable<MeetingRequest>> GetAllAsync(RequestStatus? status = null)
    {
        var requests = await _meetingRequestRepository.GetAllWithAuthorAsync(status);
        return requests.OrderByDescending(r => r.CreatedAt);
    }

    public async Task<IEnumerable<MeetingRequest>> GetPagedAsync(int page, int pageSize, RequestStatus? status = null)
    {
        return await _meetingRequestRepository.GetPagedWithAuthorAsync(page, pageSize, status);
    }

    public async Task<int> GetTotalCountAsync(RequestStatus? status = null)
    {
        return await _meetingRequestRepository.GetCountAsync(status);
    }

    public async Task<MeetingRequest?> GetByIdAsync(int id)
    {
        return await _meetingRequestRepository.GetByIdWithAuthorAsync(id);
    }

    public async Task<MeetingRequest> CreateAsync(MeetingRequest request)
    {
        return await _meetingRequestRepository.AddAsync(request);
    }

    public async Task UpdateStatusAsync(int id, RequestStatus status)
    {
        var request = await _meetingRequestRepository.GetByIdAsync(id);
        if (request == null)
            throw new InvalidOperationException($"Meeting request with ID {id} not found");
        
        request.Status = status;
        await _meetingRequestRepository.UpdateAsync(request);
    }

    public async Task DeleteAsync(int id)
    {
        var request = await _meetingRequestRepository.GetByIdAsync(id);
        if (request == null)
            throw new InvalidOperationException($"Meeting request with ID {id} not found");
        
        await _meetingRequestRepository.DeleteAsync(id);
    }
}
