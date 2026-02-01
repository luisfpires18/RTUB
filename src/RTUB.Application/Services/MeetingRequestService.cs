using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Services;

/// <summary>
/// Meeting request service implementation
/// Contains business logic for CV meeting request operations
/// </summary>
public class MeetingRequestService : IMeetingRequestService
{
    private readonly IMeetingRequestRepository _meetingRequestRepository;
    private readonly IPushNotificationFactory _pushNotificationFactory;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<MeetingRequestService> _logger;

    public MeetingRequestService(
        IMeetingRequestRepository meetingRequestRepository,
        IPushNotificationFactory pushNotificationFactory,
        IPushNotificationService pushNotificationService,
        UserManager<ApplicationUser> userManager,
        IHttpContextAccessor httpContextAccessor,
        ILogger<MeetingRequestService> logger)
    {
        _meetingRequestRepository = meetingRequestRepository;
        _pushNotificationFactory = pushNotificationFactory;
        _pushNotificationService = pushNotificationService;
        _userManager = userManager;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
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
        var createdRequest = await _meetingRequestRepository.AddAsync(request);

        try
        {
            var baseUrl = GetBaseUrl();

            var tempMeeting = new Meeting
            {
                Id = 0,
                Type = request.RequestedMeetingType,
                Title = $"Pedido: {request.Title}",
                Date = request.ProposedDateTime,
                Location = request.Location,
                Statement = request.Description,
                OrganizerUserId = request.AuthorUserId
            };

            var notification = _pushNotificationFactory.CreateMeetingNotification(tempMeeting, isReminder: false, baseUrl);

            // 1) Owners (role) - extract IDs immediately to avoid tracking issues
            var ownerUsers = await _userManager.GetUsersInRoleAsync("Owner");
            var ownerUserIds = ownerUsers.Select(u => u.Id).ToList();

            // 2) Load all users once with AsNoTracking to prevent accumulating tracked entities
            // which can cause issues during subsequent SaveChangesAsync calls
            var allUsers = await _userManager.Users.AsNoTracking().ToListAsync();

            // 3) Pick extra recipients based on meeting type (in memory, can use Positions safely)
            IEnumerable<string> positionRecipientIds = Enumerable.Empty<string>();

            switch (request.RequestedMeetingType)
            {
                case MeetingType.ConselhoVeteranos:
                    positionRecipientIds = allUsers
                        .Where(u => u.Positions != null &&
                                    u.Positions.Contains(Position.PresidenteConselhoVeteranos))
                        .Select(u => u.Id);
                    break;

                case MeetingType.AssembleiaGeralOrdinaria:
                case MeetingType.AssembleiaGeralExtraordinaria:
                    positionRecipientIds = allUsers
                        .Where(u => u.Positions != null &&
                                    u.Positions.Contains(Position.PresidenteMesaAssembleia))
                        .Select(u => u.Id);
                    break;

                default:
                    // other meeting types: only Owners (no extra positions)
                    break;
            }

            // 4) Union Owners + position-based recipients
            var recipientUserIds = ownerUserIds
                .Concat(positionRecipientIds)
                .Distinct()
                .ToList();

            // 5) Send notifications
            foreach (var userId in recipientUserIds)
            {
                await _pushNotificationService.SendToUserAsync(userId, notification);
            }
        }
        catch (Exception ex)
        {
            // Log error; notification failure must not break request creation
            _logger.LogError(ex, "Failed to send push notifications for meeting request {RequestId} (Title: {Title})",
                createdRequest.Id, createdRequest.Title);
        }

        return createdRequest;
    }

    public async Task UpdateStatusAsync(int id, RequestStatus status)
    {
        var request = await _meetingRequestRepository.GetByIdOrThrowAsync(id);

        request.Status = status;
        await _meetingRequestRepository.UpdateAsync(request);
    }

    public async Task DeleteAsync(int id)
    {
        var request = await _meetingRequestRepository.GetByIdOrThrowAsync(id);

        await _meetingRequestRepository.DeleteAsync(id);
    }

    public async Task<bool> SendReminderNotificationAsync(int id, string baseUrl)
    {
        var request = await _meetingRequestRepository.GetByIdWithAuthorAsync(id);
        if (request == null || request.Status != RequestStatus.Pending)
            return false;

        try
        {
            var notification = _pushNotificationFactory.CreatePendingMeetingRequestReminderNotification(request, baseUrl);

            // 1) Owners (role) - extract IDs immediately to avoid tracking issues
            var ownerUsers = await _userManager.GetUsersInRoleAsync("Owner");
            var ownerUserIds = ownerUsers.Select(u => u.Id).ToList();

            // 2) Load all users once with AsNoTracking to prevent accumulating tracked entities
            // which can cause issues during subsequent SaveChangesAsync calls
            var allUsers = await _userManager.Users.AsNoTracking().ToListAsync();

            // 3) Pick extra recipients based on meeting type
            IEnumerable<string> positionRecipientIds = Enumerable.Empty<string>();

            switch (request.RequestedMeetingType)
            {
                case MeetingType.ConselhoVeteranos:
                    positionRecipientIds = allUsers
                        .Where(u => u.Positions != null &&
                                    u.Positions.Contains(Position.PresidenteConselhoVeteranos))
                        .Select(u => u.Id);
                    break;

                case MeetingType.AssembleiaGeralOrdinaria:
                case MeetingType.AssembleiaGeralExtraordinaria:
                    positionRecipientIds = allUsers
                        .Where(u => u.Positions != null &&
                                    u.Positions.Contains(Position.PresidenteMesaAssembleia))
                        .Select(u => u.Id);
                    break;

                case MeetingType.ReuniaoDirecao:
                    positionRecipientIds = allUsers
                        .Where(u => u.Positions != null &&
                                    (u.Positions.Contains(Position.Magister) ||
                                     u.Positions.Contains(Position.ViceMagister)))
                        .Select(u => u.Id);
                    break;

                default:
                    break;
            }

            // 4) Union Owners + position-based recipients and get user info for logging
            var recipientUserIds = ownerUserIds
                .Concat(positionRecipientIds)
                .Distinct()
                .ToList();

            // Build a lookup for logging purposes (using the untracked allUsers list)
            var userLookup = allUsers.ToDictionary(u => u.Id, u => u.Nickname ?? u.UserName ?? u.Id);

            // 5) Send notifications
            foreach (var userId in recipientUserIds)
            {
                await _pushNotificationService.SendToUserAsync(userId, notification);
                _logger.LogInformation(
                    "Push notification sent to {Username} about meeting request '{MeetingTitle}'",
                    userLookup.GetValueOrDefault(userId, userId),
                    request.Title);
            }

            return true;
        }
        catch (Exception ex)
        {
            // Log error; reminder notification failure should not break the flow
            _logger.LogError(ex, "Failed to send reminder notification for meeting request {RequestId}", id);
            return false;
        }
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
