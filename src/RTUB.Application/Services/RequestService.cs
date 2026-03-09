using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Exceptions;


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
    private readonly IPushNotificationFactory _pushNotificationFactory;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RequestService(
        IRequestRepository requestRepository,
        IPushNotificationFactory pushNotificationFactory,
        IPushNotificationService pushNotificationService,
        UserManager<ApplicationUser> userManager,
        IHttpContextAccessor httpContextAccessor)
    {
        _requestRepository = requestRepository;
        _pushNotificationFactory = pushNotificationFactory;
        _pushNotificationService = pushNotificationService;
        _userManager = userManager;
        _httpContextAccessor = httpContextAccessor;
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

        // Send push notification to admins
        try
        {
            var baseUrl = GetBaseUrl();
            var notification = _pushNotificationFactory.CreateRequestNotification(createdRequest, baseUrl);

            // Get admin user IDs
            var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
            var ownerUsers = await _userManager.GetUsersInRoleAsync("Owner");
            var adminUserIds = adminUsers.Union(ownerUsers).Select(u => u.Id).Distinct().ToList();

            // Send to each admin
            foreach (var userId in adminUserIds)
            {
                await _pushNotificationService.SendToUserAsync(userId, notification);
            }
        }
        catch
        {
            // Log error but don't fail the operation
            // Notification is secondary to the main operation
        }

        return createdRequest;
    }

    public async Task SetRequestDateRangeAsync(int id, DateTime endDate)
    {
        var request = await _requestRepository.GetByIdOrThrowAsync(id);

        request.SetDateRange(endDate);
        await _requestRepository.UpdateAsync(request);
    }

    public async Task UpdateRequestStatusAsync(int id, RequestStatus status)
    {
        var request = await _requestRepository.GetByIdOrThrowAsync(id);

        request.UpdateStatus(status);
        await _requestRepository.UpdateAsync(request);
    }

    public async Task DeleteRequestAsync(int id)
    {
        var request = await _requestRepository.GetByIdOrThrowAsync(id);

        await _requestRepository.DeleteAsync(request);
    }

    private string GetBaseUrl()
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        if (request != null)
        {
            return $"{request.Scheme}://{request.Host}";
        }
        return "https://rtub.pt"; // Fallback
    }
}
