using System.Net;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RTUB.Application.Configuration;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using Xunit;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// The real send path (#230): WebPush builds and encrypts a real request with real VAPID keys (generated per run), and a
/// fake HttpMessageHandler plays the push service, so each provider answer can be checked without the network.
/// </summary>
public class PushDeliveryTests
{
    private const string UserId = "user-230";
    private readonly Mock<IPushSubscriptionRepository> _repository = new();
    private readonly Mock<ILogger<PushNotificationService>> _logger = new();
    private readonly FakePushService _pushService = new();
    private readonly WebPushOptions _options;

    public PushDeliveryTests()
    {
        var vapid = WebPush.VapidHelper.GenerateVapidKeys();
        _options = new WebPushOptions
        {
            Enabled = true,
            VapidSubject = "mailto:push-tests@rtub.invalid",
            VapidPublicKey = vapid.PublicKey,
            VapidPrivateKey = vapid.PrivateKey,
        };
    }

    private PushNotificationService Service() => new(_repository.Object, Mock.Of<IUserProfileRepository>(), Options.Create(_options),
        _logger.Object, new HttpClient(_pushService));

    private static readonly SendPushNotificationDto Notification = new() { Title = "T", Body = "B", Url = "/profile#push", Tag = "push-test" };

    [Fact]
    public async Task Accepted_IsSent_WithTtlUrgencyAndVapid_AndKeepsTheSubscription()
    {
        var phone = Subscription(1);
        Owns(phone);
        _pushService.Answer(HttpStatusCode.Created);

        var result = await Service().SendTestAsync(UserId, phone.Endpoint, Notification);

        result.Outcome.Should().Be(PushDeliveryOutcome.Sent);
        var request = _pushService.Requests.Should().ContainSingle().Subject;
        request.Uri.Should().Be(phone.Endpoint);
        request.Headers["TTL"].Should().Be("86400");
        request.Headers["Urgency"].Should().Be("high");
        request.Headers.Should().ContainKey("Authorization");
        request.BodyLength.Should().BeGreaterThan(0, "the payload is encrypted, never sent empty");
        _repository.Verify(r => r.DeleteAsync(It.IsAny<PushSubscription>()), Times.Never);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Gone)]
    public async Task NotFoundOrGone_RemovesThatSubscription(HttpStatusCode status)
    {
        var phone = Subscription(1);
        Owns(phone);
        _pushService.Answer(status);

        var result = await Service().SendTestAsync(UserId, phone.Endpoint, Notification);

        result.Should().Be(new PushSendResult(PushDeliveryOutcome.Gone, (int)status));
        _repository.Verify(r => r.DeleteAsync(phone), Times.Once);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, PushDeliveryOutcome.Rejected)]
    [InlineData(HttpStatusCode.Unauthorized, PushDeliveryOutcome.Rejected)]
    [InlineData(HttpStatusCode.Forbidden, PushDeliveryOutcome.Rejected)]
    [InlineData(HttpStatusCode.RequestEntityTooLarge, PushDeliveryOutcome.Rejected)]
    [InlineData(HttpStatusCode.InternalServerError, PushDeliveryOutcome.ProviderError)]
    [InlineData(HttpStatusCode.ServiceUnavailable, PushDeliveryOutcome.ProviderError)]
    public async Task OtherProviderErrors_AreReported_AndNeverRemoveTheSubscription(HttpStatusCode status, PushDeliveryOutcome expected)
    {
        var phone = Subscription(1);
        Owns(phone);
        _pushService.Answer(status, "{\"reason\":\"BadJwtToken\"}");

        var result = await Service().SendTestAsync(UserId, phone.Endpoint, Notification);

        result.Should().Be(new PushSendResult(expected, (int)status));
        _pushService.Requests.Should().ContainSingle();
        _repository.Verify(r => r.DeleteAsync(It.IsAny<PushSubscription>()), Times.Never);
        LoggedWarning("BadJwtToken").Should().BeTrue("the push service's reason makes a refusal diagnosable");
        LoggedWarning(phone.P256dh).Should().BeFalse();
        LoggedWarning(phone.Auth).Should().BeFalse();
        LoggedWarning(phone.Endpoint).Should().BeFalse("only the push service host is logged, not the endpoint");
    }

    [Fact]
    public async Task UnreachablePushService_IsRetried_ThenANetworkError_AndKeepsTheSubscription()
    {
        var phone = Subscription(1);
        Owns(phone);
        _pushService.Throw(new HttpRequestException("connection refused"));

        var result = await Service().SendTestAsync(UserId, phone.Endpoint, Notification);

        result.Outcome.Should().Be(PushDeliveryOutcome.NetworkError);
        _pushService.Requests.Should().HaveCount(4, "1 attempt + 3 retries");
        _repository.Verify(r => r.DeleteAsync(It.IsAny<PushSubscription>()), Times.Never);
    }

    [Fact]
    public async Task Test_OnlyEverTargetsTheCallersOwnEndpoint()
    {
        Owns(Subscription(1));

        var result = await Service().SendTestAsync(UserId, "https://fcm.googleapis.com/fcm/send/someone-else", Notification);

        result.Outcome.Should().Be(PushDeliveryOutcome.NotRegistered);
        _pushService.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Test_WithoutVapidKeys_SendsNothing()
    {
        var phone = Subscription(1);
        Owns(phone);
        _options.VapidPrivateKey = "";

        (await Service().SendTestAsync(UserId, phone.Endpoint, Notification)).Outcome.Should().Be(PushDeliveryOutcome.NotConfigured);
        _pushService.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task SendToUser_ReachesEveryDevice_EvenWhenRemovingAGoneOneFails()
    {
        var old = Subscription(1);
        var phone = Subscription(2);
        Owns(old, phone);
        _pushService.Answer(HttpStatusCode.Gone);
        _pushService.Answer(HttpStatusCode.Created);
        _repository.Setup(r => r.DeleteAsync(old)).ThrowsAsync(new InvalidOperationException("database is locked"));

        await Service().SendToUserAsync(UserId, Notification);

        _pushService.Requests.Should().HaveCount(2);
        LoggedWarning("Could not remove gone push subscription 1").Should().BeTrue();
        LoggedWarning("stale removed").Should().BeFalse("nothing failed, so the summary is not a warning");
    }

    [Fact]
    public async Task SendToUser_WarnsWithTheCounts_WhenADeviceFails()
    {
        Owns(Subscription(1), Subscription(2));
        _pushService.Answer(HttpStatusCode.Created);
        _pushService.Answer(HttpStatusCode.Forbidden);

        await Service().SendToUserAsync(UserId, Notification);

        LoggedWarning("1 sent, 0 stale removed, 1 failed of 2").Should().BeTrue();
    }

    [Fact]
    public async Task SendToSelectedUsers_CountsOnlyAcceptedPushesAsSent()
    {
        var phone = Subscription(1);
        var laptop = Subscription(2);
        var someoneElse = Subscription(3, "other-user");
        _repository.Setup(r => r.GetAllActiveAsync()).ReturnsAsync([phone, laptop, someoneElse]);
        _pushService.Answer(HttpStatusCode.Created);
        _pushService.Answer(HttpStatusCode.Forbidden);

        var (sent, failed) = await Service().SendToSelectedUsersAsync([UserId], Notification);

        (sent, failed).Should().Be((1, 1));
        _pushService.Requests.Select(r => r.Uri).Should().Equal(phone.Endpoint, laptop.Endpoint);
        _repository.Verify(r => r.DeleteAsync(It.IsAny<PushSubscription>()), Times.Never);
    }

    // ---------- helpers ----------

    private void Owns(params PushSubscription[] subscriptions) =>
        _repository.Setup(r => r.GetByUserIdAsync(UserId)).ReturnsAsync(subscriptions);

    /// <summary>A subscription with real browser-shaped keys (P-256 public key, 16-byte auth), so encryption runs.</summary>
    private static PushSubscription Subscription(int id, string userId = UserId)
    {
        using var ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var q = ecdh.ExportParameters(false).Q;
        return new PushSubscription
        {
            Id = id,
            UserId = userId,
            Endpoint = $"https://fcm.googleapis.com/fcm/send/device-{id}-{Guid.NewGuid():N}",
            P256dh = PushNotificationService.NormalizeBase64Url(Convert.ToBase64String([0x04, .. q.X!, .. q.Y!])),
            Auth = PushNotificationService.NormalizeBase64Url(Convert.ToBase64String(RandomNumberGenerator.GetBytes(16))),
            User = new ApplicationUser { Id = userId, Nickname = "Tester", FirstName = "T", LastName = "T" },
        };
    }

    private bool LoggedWarning(string text) => _logger.Invocations.Any(i =>
        i.Method.Name == nameof(ILogger.Log) && (LogLevel)i.Arguments[0] == LogLevel.Warning && i.Arguments[2]?.ToString()?.Contains(text) == true);

    /// <summary>Plays the push service: answers queued statuses in order (the last one repeats) or throws.</summary>
    private sealed class FakePushService : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, string Body)> _answers = new();
        private (HttpStatusCode Status, string Body) _last = (HttpStatusCode.Created, "");
        private Exception? _throw;

        public List<(string Uri, Dictionary<string, string> Headers, long BodyLength)> Requests { get; } = [];

        public void Answer(HttpStatusCode status, string body = "") => _answers.Enqueue((status, body));

        public void Throw(Exception exception) => _throw = exception;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var headers = request.Headers.Concat(request.Content?.Headers ?? Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>())
                .ToDictionary(h => h.Key, h => string.Join(",", h.Value));
            var body = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            Requests.Add((request.RequestUri!.ToString(), headers, body.Length));

            if (_throw is not null) throw _throw;
            if (_answers.Count > 0) _last = _answers.Dequeue();
            return new HttpResponseMessage(_last.Status) { Content = new StringContent(_last.Body) };
        }
    }
}
