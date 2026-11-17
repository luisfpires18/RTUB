using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;

namespace RTUB.Integration.Tests.Api;

/// <summary>The host with push and email replaced by recording fakes: nothing reaches a device or a mailbox.</summary>
public sealed class OwnerApiFactory : TestWebApplicationFactory
{
    public Mock<IPushNotificationService> Push { get; } = new();
    public Mock<IEmailNotificationService> Email { get; } = new();

    /// <summary>The accounts the fake push service reports as subscribed; tests add their own.</summary>
    public System.Collections.Concurrent.ConcurrentBag<string> PushSubscribed { get; } = new();

    public OwnerApiFactory()
    {
        Push.Setup(p => p.GetSubscribedUserIdsAsync()).ReturnsAsync(() => PushSubscribed.ToArray().AsEnumerable());
        Push.Setup(p => p.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<SendPushNotificationDto>()))
            .ReturnsAsync((IEnumerable<string> ids, SendPushNotificationDto _) => (ids.Count(), 0));
        Email.Setup(e => e.SendAnnouncementEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(),
                It.IsAny<Dictionary<string, (string, string)>>(), It.IsAny<IProgress<EmailSendProgress>?>()))
            .ReturnsAsync((string _, string _, List<string> to, Dictionary<string, (string, string)> _, IProgress<EmailSendProgress>? _) => (true, to.Count, (string?)null));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPushNotificationService>();
            services.AddSingleton(Push.Object);
            services.RemoveAll<IEmailNotificationService>();
            services.AddSingleton(Email.Object);
        });
    }
}
