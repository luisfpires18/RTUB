using RTUB.Application.Interfaces;
using RTUB.Application.Data;
using RTUB.Application.Extensions;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;
using RTUB.Core.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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

            var notification = _pushNotificationFactory.CreateMeetingNotification(tempMeeting, baseUrl);

            // 1) Owners (role)
            var ownerUsers = await _userManager.GetUsersInRoleAsync("Owner");

            // 2) Load all users once (EF async, SQL side)
            var allUsers = await _userManager.Users.ToListAsync();

            // 3) Pick extra recipients based on meeting type (in memory, can use Positions safely)
            IEnumerable<ApplicationUser> positionRecipients = Enumerable.Empty<ApplicationUser>();

            switch (request.RequestedMeetingType)
            {
                case MeetingType.ConselhoVeteranos:
                    positionRecipients = allUsers
                        .Where(u => u.Positions != null &&
                                    u.Positions.Contains(Position.PresidenteConselhoVeteranos));
                    break;

                case MeetingType.AssembleiaGeralOrdinaria:
                case MeetingType.AssembleiaGeralExtraordinaria:
                    positionRecipients = allUsers
                        .Where(u => u.Positions != null &&
                                    u.Positions.Contains(Position.PresidenteMesaAssembleia));
                    break;

                default:
                    // other meeting types: only Owners (no extra positions)
                    break;
            }

            // 4) Union Owners + position-based recipients
            var recipientUserIds = ownerUsers
                .Concat(positionRecipients)
                .Select(u => u.Id)
                .Distinct()
                .ToList();

            // 5) Send notifications
            foreach (var userId in recipientUserIds)
            {
                await _pushNotificationService.SendToUserAsync(userId, notification);
            }
        }
        catch
        {
            // TODO: log error; notification failure must not break request creation
        }

        return createdRequest;
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

    public async Task<bool> SendReminderNotificationAsync(int id, string baseUrl)
    {
        var request = await _meetingRequestRepository.GetByIdWithAuthorAsync(id);
        if (request == null || request.Status != RequestStatus.Pending)
            return false;

        try
        {
            var notification = _pushNotificationFactory.CreatePendingMeetingRequestReminderNotification(request, baseUrl);

            // 1) Owners (role)
            var ownerUsers = await _userManager.GetUsersInRoleAsync("Owner");

            // 2) Load all users once
            var allUsers = await _userManager.Users.ToListAsync();

            // 3) Pick extra recipients based on meeting type
            IEnumerable<ApplicationUser> positionRecipients = Enumerable.Empty<ApplicationUser>();

            switch (request.RequestedMeetingType)
            {
                case MeetingType.ConselhoVeteranos:
                    positionRecipients = allUsers
                        .Where(u => u.Positions != null &&
                                    u.Positions.Contains(Position.PresidenteConselhoVeteranos));
                    break;

                case MeetingType.AssembleiaGeralOrdinaria:
                case MeetingType.AssembleiaGeralExtraordinaria:
                    positionRecipients = allUsers
                        .Where(u => u.Positions != null &&
                                    u.Positions.Contains(Position.PresidenteMesaAssembleia));
                    break;

                case MeetingType.ReuniaoDirecao:
                    positionRecipients = allUsers
                        .Where(u => u.Positions != null &&
                                    (u.Positions.Contains(Position.Magister) ||
                                     u.Positions.Contains(Position.ViceMagister)));
                    break;

                default:
                    break;
            }

            // 4) Union Owners + position-based recipients
            var recipients = ownerUsers
                .Concat(positionRecipients)
                .DistinctBy(u => u.Id)
                .ToList();

            // 5) Send notifications
            foreach (var recipient in recipients)
            {
                await _pushNotificationService.SendToUserAsync(recipient.Id, notification);
                _logger.LogInformation(
                    "Push notification sent to {Username} about meeting request '{MeetingTitle}'",
                    recipient.Nickname ?? recipient.UserName ?? recipient.Id,
                    request.Title);
            }

            return true;
        }
        catch
        {
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
