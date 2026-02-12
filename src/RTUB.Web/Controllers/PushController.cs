using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;

namespace RTUB.Controllers;

/// <summary>
/// Controller for managing Web Push notifications
/// Implements authorization logic: OWNER role OR WebPush:Enabled = true
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PushController : ControllerBase
{
    private readonly IPushNotificationService _pushNotificationService;
    private readonly WebPushOptions _webPushOptions;
    private readonly ILogger<PushController> _logger;

    public PushController(
        IPushNotificationService pushNotificationService,
        IOptions<WebPushOptions> webPushOptions,
        ILogger<PushController> logger)
    {
        _pushNotificationService = pushNotificationService;
        _webPushOptions = webPushOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Checks if the current user has access to Web Push features
    /// Owner users always have access, non-Owner users need WebPush:Enabled = true
    /// </summary>
    private bool HasWebPushAccess()
    {
        return User.IsInRole("Owner") || _webPushOptions.Enabled;
    }

    /// <summary>
    /// Gets the Web Push feature status for the current user
    /// Returns whether the user can use Web Push and configuration details
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var hasAccess = HasWebPushAccess();
        var isConfigured = _pushNotificationService.IsConfigured();

        return Ok(new PushStatusDto
        {
            IsEnabled = hasAccess,
            IsConfigured = isConfigured,
            VapidPublicKey = hasAccess && isConfigured ? _pushNotificationService.GetVapidPublicKey() : null
        });
    }

    /// <summary>
    /// Subscribes the current user to push notifications
    /// </summary>
    /// <param name="subscription">The push subscription details from the browser</param>
    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] PushSubscriptionDto subscription)
    {
        if (!HasWebPushAccess())
        {
            return Forbid();
        }

        if (!_pushNotificationService.IsConfigured())
        {
            return BadRequest(new { error = "Web Push n�o est� configurado no servidor" });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var userName = User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Name);

        try
        {
            var userAgent = Request.Headers.UserAgent.ToString();

            await _pushNotificationService.SubscribeAsync(userId, subscription, userAgent, userName);

            _logger.LogInformation("User {userName} subscribed to push notifications", userName);

            return Ok(new { message = "Inscrito com sucesso para notifica��es push" });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid subscription data from user {UserName}", userName);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error subscribing user {UserName} to push notifications", userName);
            return StatusCode(500, new { error = "Failed to subscribe to push notifications" });
        }
    }

    /// <summary>
    /// Unsubscribes from push notifications
    /// </summary>
    /// <param name="request">The unsubscribe request containing the endpoint</param>
    [HttpPost("unsubscribe")]
    public async Task<IActionResult> Unsubscribe([FromBody] UnsubscribeRequest request)
    {
        if (!HasWebPushAccess())
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Endpoint))
        {
            return BadRequest(new { error = "Endpoint is required" });
        }

        try
        {
            await _pushNotificationService.UnsubscribeAsync(request.Endpoint);

            var userName = User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Name);

            _logger.LogInformation("User {userName} unsubscribed from push notifications", userName);

            return Ok(new { message = "A inscri��o de notifica��es push foi cancelada com sucesso." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unsubscribing from push notifications");
            return StatusCode(500, new { error = "Failed to unsubscribe from push notifications" });
        }
    }

    /// <summary>
    /// Broadcasts a push notification to all subscribed users
    /// Only Owner role can broadcast
    /// </summary>
    /// <param name="notification">The notification to broadcast</param>
    [HttpPost("broadcast")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> Broadcast([FromBody] SendPushNotificationDto notification)
    {
        if (!_pushNotificationService.IsConfigured())
        {
            return BadRequest(new { error = "Web Push is not configured on the server" });
        }

        try
        {
            await _pushNotificationService.BroadcastAsync(notification);

            var userName = User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Name);

            _logger.LogInformation("User {userName} broadcasted push notification to all subscribers", userName);

            return Ok(new { message = "Notification broadcasted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting push notification");
            return StatusCode(500, new { error = "Failed to broadcast notification" });
        }
    }

    /// <summary>
    /// Sends a push notification to selected users
    /// Returns delivery results with success/failure counts
    /// </summary>
    /// <param name="request">The notification and target user IDs</param>
    [HttpPost("send-to-selected")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> SendToSelected([FromBody] SendToSelectedRequest request)
    {
        if (!_pushNotificationService.IsConfigured())
        {
            return BadRequest(new { error = "Web Push is not configured on the server" });
        }

        if (request.UserIds == null || !request.UserIds.Any())
        {
            return BadRequest(new { error = "No users selected" });
        }

        try
        {
            var (sent, failed) = await _pushNotificationService.SendToSelectedUsersAsync(
                request.UserIds, request.Notification);

            var userName = User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Name);

            _logger.LogInformation(
                "User {userName} sent push notification to {UserCount} selected users: {Sent} sent, {Failed} failed",
                userName, request.UserIds.Count(), sent, failed);

            return Ok(new
            {
                message = $"Notificação enviada: {sent} com sucesso, {failed} falharam",
                sent,
                failed,
                totalUsers = request.UserIds.Count()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending push notification to selected users");
            return StatusCode(500, new { error = "Failed to send notification to selected users" });
        }
    }
}

/// <summary>
/// Request model for sending to selected users via the API
/// </summary>
public class SendToSelectedRequest
{
    public IEnumerable<string> UserIds { get; set; } = Enumerable.Empty<string>();
    public SendPushNotificationDto Notification { get; set; } = new();
}
