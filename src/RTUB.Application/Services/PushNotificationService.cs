using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using WebPush;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing Web Push notifications
/// Handles subscription management and sending push notifications using VAPID
/// </summary>
public partial class PushNotificationService : IPushNotificationService
{
    private const int MaxRetryAttempts = 3;
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromMilliseconds(200);

    private readonly IPushSubscriptionRepository _subscriptionRepository;
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly WebPushOptions _options;
    private readonly ILogger<PushNotificationService> _logger;
    private readonly WebPushClient _webPushClient;
    private readonly NotificationInbox? _inbox;
    private readonly IPushPreferenceStore? _preferences;

    /// <summary>The named <see cref="HttpClient"/> every send goes through; the host registers it (task 054).</summary>
    public const string HttpClientName = "WebPush";

    public PushNotificationService(
        IPushSubscriptionRepository subscriptionRepository,
        IUserProfileRepository userProfileRepository,
        IOptions<WebPushOptions> options,
        ILogger<PushNotificationService> logger)
        : this(subscriptionRepository, userProfileRepository, options, logger, (HttpClient?)null)
    {
    }

    /// <summary>The host's constructor: the pooled client from <see cref="IHttpClientFactory"/> (task 054).</summary>
    public PushNotificationService(
        IPushSubscriptionRepository subscriptionRepository,
        IUserProfileRepository userProfileRepository,
        IOptions<WebPushOptions> options,
        ILogger<PushNotificationService> logger,
        IHttpClientFactory httpClientFactory,
        NotificationInbox inbox,
        IPushPreferenceStore preferences)
        : this(subscriptionRepository, userProfileRepository, options, logger, httpClientFactory.CreateClient(HttpClientName), inbox, preferences)
    {
    }

    /// <summary>Tests pass an <see cref="HttpClient"/> over a fake handler, so no real push service is called.</summary>
    internal PushNotificationService(
        IPushSubscriptionRepository subscriptionRepository,
        IUserProfileRepository userProfileRepository,
        IOptions<WebPushOptions> options,
        ILogger<PushNotificationService> logger,
        HttpClient? httpClient,
        NotificationInbox? inbox = null,
        IPushPreferenceStore? preferences = null)
    {
        _inbox = inbox;
        _preferences = preferences;
        _subscriptionRepository = subscriptionRepository;
        _userProfileRepository = userProfileRepository;
        _options = options.Value;
        _logger = logger;
        _webPushClient = httpClient is null ? new WebPushClient() : new WebPushClient(httpClient);

        // Configure VAPID details if available and valid
        if (_options.IsConfigured())
        {
            try
            {
                _webPushClient.SetVapidDetails(
                    _options.VapidSubject,
                    _options.VapidPublicKey,
                    _options.VapidPrivateKey);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid VAPID configuration. Web Push will not be functional.");
            }
        }
    }
}
