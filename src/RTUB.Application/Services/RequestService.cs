using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;


namespace RTUB.Application.Services;

/// <summary>
/// Request service implementation
/// Contains business logic for request operations
/// Follows Single Responsibility and Dependency Inversion principles
/// </summary>
public class RequestService : IRequestService
{
    private readonly ApplicationDbContext _context;
    private readonly IEmailNotificationService _emailNotificationService;

    public RequestService(ApplicationDbContext context, IEmailNotificationService emailNotificationService)
    {
        _context = context;
        _emailNotificationService = emailNotificationService;
    }

    public async Task<Request?> GetRequestByIdAsync(int id)
    {
        return await _context.Requests.FindAsync(id);
    }

    public async Task<IEnumerable<Request>> GetAllRequestsAsync()
    {
        return await _context.Requests.ToListAsync();
    }

    public async Task<IEnumerable<Request>> GetPendingRequestsAsync()
    {
        return await _context.Requests
            .Where(r => r.Status == RequestStatus.Pending)
            .OrderBy(r => r.PreferredDate)
            .ToListAsync();
    }

    public async Task<Request> CreateRequestAsync(string name, string email, string phone, string eventType, DateTime preferredDate, string location, string message)
    {
        var request = Request.Create(name, email, phone, eventType, preferredDate, location, message);
        _context.Requests.Add(request);
        await _context.SaveChangesAsync();

        // Send notification for new request
        await _emailNotificationService.SendNewRequestNotificationAsync(request.Id, name, email, eventType);

        return request;
    }

    public async Task SetRequestDateRangeAsync(int id, DateTime endDate)
    {
        var request = await _context.Requests.FindAsync(id);
        if (request == null)
            throw new InvalidOperationException($"Request with ID {id} not found");

        request.SetDateRange(endDate);
        _context.Requests.Update(request);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateRequestStatusAsync(int id, RequestStatus status)
    {
        var request = await _context.Requests.FindAsync(id);
        if (request == null)
            throw new InvalidOperationException($"Request with ID {id} not found");

        var oldStatus = request.Status;
        request.UpdateStatus(status);
        _context.Requests.Update(request);
        await _context.SaveChangesAsync();

        // Send notification when status changes
        if (oldStatus != status)
        {
            await _emailNotificationService.SendRequestStatusChangedAsync(
                request.Id, request.Name, request.Email, oldStatus, status);
        }
    }

    public async Task DeleteRequestAsync(int id)
    {
        var request = await _context.Requests.FindAsync(id);
        if (request == null)
            throw new InvalidOperationException($"Request with ID {id} not found");

        _context.Requests.Remove(request);
        await _context.SaveChangesAsync();
    }
}
