using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RTUB.Application.Configuration;
using RTUB.Application.DTOs;
using RTUB.Application.Factories;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Xunit;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Task 082 (#263): the push preference is enforced once, in <see cref="PushNotificationService"/>, for every send path
/// (one member, selected members, broadcast). A muted member's devices get nothing; the inbox copy is decided before and
/// is not this test's concern (NotificationInboxTests). The mandatory key and a push without a key mute nobody. The
/// last test keeps new factory sends mapped: every notification the factory builds names a catalogue key.
/// </summary>
public class PushPreferenceFilterTests
{
    private const string Muted = "user-muted";
    private const string Listening = "user-listening";

    private readonly Mock<IPushSubscriptionRepository> _repository = new();
    private readonly Mock<IPushPreferenceStore> _preferences = new();
    private readonly FakePushService _pushService = new();
    private readonly WebPushOptions _options;

    public PushPreferenceFilterTests()
    {
        var vapid = WebPush.VapidHelper.GenerateVapidKeys();
        _options = new WebPushOptions
        {
            Enabled = true,
            VapidSubject = "mailto:push-tests@rtub.invalid",
            VapidPublicKey = vapid.PublicKey,
            VapidPrivateKey = vapid.PrivateKey,
        };
        _preferences.Setup(p => p.MutedUserIdsAsync(It.IsAny<string>())).ReturnsAsync(new HashSet<string>());
        _preferences.Setup(p => p.MutedUserIdsAsync(PushPreferenceCatalog.EventsNew)).ReturnsAsync(new HashSet<string> { Muted });
    }

    private PushNotificationService Service() => new(_repository.Object, Mock.Of<IUserProfileRepository>(), Options.Create(_options),
        Mock.Of<ILogger<PushNotificationService>>(), new HttpClient(_pushService), null, _preferences.Object);

    private static SendPushNotificationDto Push(string? key) => new() { Title = "T", Body = "B", Url = "/events/1", Tag = "event-1", PreferenceKey = key };

    [Fact]
    public async Task SendToUser_SkipsAMemberWhoTurnedTheKeyOff_AndSendsToOneWhoDidNot()
    {
        var mutedPhone = Subscription(1, Muted);
        var listeningPhone = Subscription(2, Listening);
        _repository.Setup(r => r.GetByUserIdAsync(Muted)).ReturnsAsync([mutedPhone]);
        _repository.Setup(r => r.GetByUserIdAsync(Listening)).ReturnsAsync([listeningPhone]);

        await Service().SendToUserAsync(Muted, Push(PushPreferenceCatalog.EventsNew));
        _pushService.Requests.Should().BeEmpty("the member turned events.new off");

        await Service().SendToUserAsync(Listening, Push(PushPreferenceCatalog.EventsNew));
        _pushService.Requests.Should().ContainSingle().Which.Should().Be(listeningPhone.Endpoint);
    }

    [Fact]
    public async Task SendToSelectedUsers_LeavesTheMutedMembersDevicesOut()
    {
        var mutedPhone = Subscription(1, Muted);
        var listeningPhone = Subscription(2, Listening);
        _repository.Setup(r => r.GetAllActiveAsync()).ReturnsAsync([mutedPhone, listeningPhone]);

        var (sent, failed) = await Service().SendToSelectedUsersAsync([Muted, Listening], Push(PushPreferenceCatalog.EventsNew));

        (sent, failed).Should().Be((1, 0));
        _pushService.Requests.Should().ContainSingle().Which.Should().Be(listeningPhone.Endpoint);
    }

    [Fact]
    public async Task Broadcast_LeavesTheMutedMembersDevicesOut()
    {
        var mutedPhone = Subscription(1, Muted);
        var listeningPhone = Subscription(2, Listening);
        _repository.Setup(r => r.GetAllActiveAsync()).ReturnsAsync([mutedPhone, listeningPhone]);

        await Service().BroadcastAsync(Push(PushPreferenceCatalog.EventsNew));

        _pushService.Requests.Should().ContainSingle().Which.Should().Be(listeningPhone.Endpoint);
    }

    [Fact]
    public async Task AnotherKey_TheMandatoryKey_OrNoKey_MutesNobody()
    {
        var mutedPhone = Subscription(1, Muted);
        _repository.Setup(r => r.GetByUserIdAsync(Muted)).ReturnsAsync([mutedPhone]);

        await Service().SendToUserAsync(Muted, Push(PushPreferenceCatalog.EventsReminder));
        await Service().SendToUserAsync(Muted, Push(PushPreferenceCatalog.PendingActions));
        await Service().SendToUserAsync(Muted, Push(null));

        _pushService.Requests.Should().HaveCount(3);
        _preferences.Verify(p => p.MutedUserIdsAsync(It.IsAny<string>()), Times.Exactly(2), "a push without a key never asks");
    }

    [Fact]
    public async Task WithoutAStore_NothingIsFiltered()
    {
        var mutedPhone = Subscription(1, Muted);
        _repository.Setup(r => r.GetByUserIdAsync(Muted)).ReturnsAsync([mutedPhone]);
        var service = new PushNotificationService(_repository.Object, Mock.Of<IUserProfileRepository>(), Options.Create(_options),
            Mock.Of<ILogger<PushNotificationService>>(), new HttpClient(_pushService));

        await service.SendToUserAsync(Muted, Push(PushPreferenceCatalog.EventsNew));

        _pushService.Requests.Should().ContainSingle();
    }

    [Fact]
    public void Catalog_HasExactlyOneMandatoryKey_AndUniqueKeys()
    {
        PushPreferenceCatalog.Items.Where(i => i.Mandatory).Select(i => i.Key).Should().Equal(PushPreferenceCatalog.PendingActions);
        PushPreferenceCatalog.Items.Select(i => i.Key).Should().OnlyHaveUniqueItems();
        PushPreferenceCatalog.Items.Should().OnlyContain(i => i.Key.Length <= 64 && i.Label.Length > 0 && i.Group.Length > 0);
    }

    /// <summary>
    /// Every factory method is called with plain arguments; each notification must carry a catalogue key, except the
    /// /profile test push. A new factory method without a key fails here, before it can reach members unfiltered.
    /// </summary>
    [Fact]
    public void EveryFactoryNotification_NamesACatalogueKey_ExceptTheTestPush()
    {
        var factory = new PushNotificationFactory();
        var unmapped = new List<string>();
        foreach (var method in typeof(IPushNotificationFactory).GetMethods().Where(m => m.ReturnType == typeof(SendPushNotificationDto)))
        {
            foreach (var arguments in ArgumentSets(method))
            {
                var dto = (SendPushNotificationDto)method.Invoke(factory, arguments)!;
                var label = $"{method.Name}({string.Join(", ", arguments.Select(a => a is Enum or bool ? a.ToString() : a?.GetType().Name))})";
                if (method.Name == nameof(IPushNotificationFactory.CreatePushTestNotification))
                {
                    dto.PreferenceKey.Should().BeNull(label);
                }
                else if (dto.PreferenceKey is null || !PushPreferenceCatalog.IsKnown(dto.PreferenceKey))
                {
                    unmapped.Add(label);
                }
            }
        }

        unmapped.Should().BeEmpty("every push kind must map to a key in PushPreferenceCatalog (docs/push-notifications.md)");
    }

    /// <summary>One argument set per enum / bool combination, so every branch of a method is built.</summary>
    private static IEnumerable<object?[]> ArgumentSets(MethodInfo method)
    {
        var parameters = method.GetParameters();
        IEnumerable<object?[]> sets = [[]];
        foreach (var parameter in parameters)
        {
            var choices = Choices(parameter.ParameterType).ToList();
            sets = sets.SelectMany(set => choices.Select(choice => set.Append(choice).ToArray())).ToList();
        }

        return sets;
    }

    private static IEnumerable<object?> Choices(Type type)
    {
        var inner = Nullable.GetUnderlyingType(type) ?? type;
        if (inner == typeof(bool)) return [false, true];
        if (inner.IsEnum) return Enum.GetValues(inner).Cast<object>();
        if (inner == typeof(string)) return ["x"];
        if (inner == typeof(int)) return [1];
        if (inner == typeof(decimal)) return [1m];
        if (inner == typeof(DateTime)) return [new DateTime(2026, 11, 7, 10, 0, 0, DateTimeKind.Utc)];
        if (inner == typeof(Event)) return [new Event { Id = 1, Name = "x", Location = "x", Date = DateTime.UtcNow.AddDays(1) }];
        if (inner == typeof(Rehearsal)) return [new Rehearsal { Id = 1, Date = DateTime.UtcNow.AddDays(1) }];
        if (inner == typeof(Meeting)) return [new Meeting { Id = 0, Title = "x", Date = DateTime.UtcNow.AddDays(1) }, new Meeting { Id = 1, Title = "x", Date = DateTime.UtcNow.AddDays(1) }];
        if (inner == typeof(MeetingRequest)) return [new MeetingRequest { Id = 1 }];
        if (inner == typeof(Request)) return [new Request { Id = 1 }];
        if (inner == typeof(Song)) return [new Song { Id = 1, Title = "x" }];
        throw new InvalidOperationException($"No sample value for {type.Name}; add one to Choices()");
    }

    private static PushSubscription Subscription(int id, string userId)
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
            User = new ApplicationUser { Id = userId, Nickname = userId, FirstName = "T", LastName = "T" },
        };
    }

    private sealed class FakePushService : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("") });
        }
    }
}
