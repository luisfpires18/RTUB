using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RTUB.Application.Configuration;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

// Pending request reminders: meeting requests.

public partial class PendingRequestReminderService
{
    private async Task SendMeetingRequestRemindersAsync(
        IMeetingRequestRepository meetingRequestRepository,
        IPushNotificationService pushNotificationService,
        IPushNotificationFactory pushNotificationFactory,
        UserManager<ApplicationUser> userManager,
        CancellationToken cancellationToken)
    {
        var pendingRequests = (await meetingRequestRepository.GetPendingWithAuthorAsync()).ToList();

        if (!pendingRequests.Any())
        {
            _logger.LogInformation("No pending meeting requests found");
            return;
        }

        _logger.LogInformation("Found {Count} pending meeting requests", pendingRequests.Count);

        var today = PortugalTime.Today(_time);

        // 1) Automatically expire pending requests whose proposed date has already passed.
        // These should move out of "Pendente" so they can no longer be accepted.
        var expiredRequests = pendingRequests
            .Where(r => r.ProposedDateTime.Date < today)
            .ToList();

        if (expiredRequests.Any())
        {
            foreach (var request in expiredRequests)
            {
                if (cancellationToken.IsCancellationRequested) break;

                // Use "Rejected" as the non-answer state to keep enum surface small.
                request.Status = RequestStatus.Rejected;
                await meetingRequestRepository.UpdateAsync(request);
                _logger.LogInformation(
                    "Auto-expired meeting request {RequestId} ('{Title}') because the proposed date {ProposedDate} has passed.",
                    request.Id,
                    request.Title,
                    request.ProposedDateTime);

                if (!string.IsNullOrWhiteSpace(request.AuthorUserId))
                {
                    try
                    {
                        var notification = pushNotificationFactory.CreateMeetingRequestRejectedNotification(
                            request,
                            isExpired: true);
                        await pushNotificationService.SendToUserAsync(request.AuthorUserId, notification);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Failed to send auto-expired meeting request notification for request {RequestId}",
                            request.Id);
                    }
                }
            }

            // Remove expired ones from the list we will send reminders for
            pendingRequests = pendingRequests
                .Where(r => r.ProposedDateTime.Date >= today)
                .ToList();
        }

        if (!pendingRequests.Any())
        {
            _logger.LogInformation("All pending meeting requests are now expired; no reminders to send.");
            return;
        }

        // Load all users once for position-based filtering with AsNoTracking to prevent tracking issues
        var allUsers = await userManager.Users.AsNoTracking().ToListAsync(cancellationToken);

        // Get owner user IDs immediately to avoid tracking issues
        var ownerUsers = await userManager.GetUsersInRoleAsync("Owner");
        var ownerUserIds = ownerUsers.Select(u => u.Id).ToList();

        // Build a lookup for logging purposes
        var userLookup = allUsers.ToDictionary(u => u.Id, u => u.Nickname ?? u.UserName ?? u.Id);

        foreach (var request in pendingRequests)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var notification = pushNotificationFactory.CreatePendingMeetingRequestReminderNotification(request);

            // Determine recipients based on meeting type
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

            // Union Owners + position-based recipients
            var recipientUserIds = ownerUserIds
                .Concat(positionRecipientIds)
                .Distinct()
                .ToList();

            foreach (var userId in recipientUserIds)
            {
                if (cancellationToken.IsCancellationRequested) break;
                await pushNotificationService.SendToUserAsync(userId, notification);
                _logger.LogInformation(
                    "Push notification sent to {Username} about meeting request '{MeetingTitle}'",
                    userLookup.GetValueOrDefault(userId, userId),
                    request.Title);
            }

            _logger.LogInformation(
                "Sent pending meeting request reminder for '{Title}' to {Count} recipients",
                request.Title,
                recipientUserIds.Count);
        }
    }
}
