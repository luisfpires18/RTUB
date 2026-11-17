// MeetingsApiFactory: test class moved here from MeetingsApiTests.cs.
using System.Collections.Concurrent;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;

namespace RTUB.Integration.Tests.Api;
/// <summary>The host with push, email and document storage replaced by recording fakes: nothing reaches a device, a mailbox or R2.</summary>
public sealed class MeetingsApiFactory : TestWebApplicationFactory
{
    public Mock<IPushNotificationService> Push { get; } = new();
    public Mock<IEmailNotificationService> Email { get; } = new();
    public Mock<IDocumentStorageService> Documents { get; } = new();

    /// <summary>The members the fake push service reports as subscribed; tests add their own.</summary>
    public ConcurrentBag<string> Subscribed { get; } = new();

    public MeetingsApiFactory()
    {
        Push.Setup(p => p.GetSubscribedUserIdsAsync()).ReturnsAsync(() => Subscribed.ToArray().AsEnumerable());
        Email.Setup(e => e.SendMeetingNotificationAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(),
                It.IsAny<Dictionary<string, (string, string)>?>(), It.IsAny<IProgress<EmailSendProgress>?>()))
            .ReturnsAsync((true, 1, (string?)null));
        Documents.Setup(d => d.UploadDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync((string folder, string name, Stream _, string _) => folder + name);
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
            services.RemoveAll<IDocumentStorageService>();
            services.AddSingleton(Documents.Object);
        });
    }
}
