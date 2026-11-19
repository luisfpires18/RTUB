// EventsApiFactory: test class moved here from EventsApiTests.cs.
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;

namespace RTUB.Integration.Tests.Api;
/// <summary>
/// The test host with push, email and image storage replaced by recording fakes: nothing is ever
/// sent to anyone or written to R2.
/// </summary>
public sealed class EventsApiFactory : TestWebApplicationFactory
{
    public Mock<IPushNotificationService> Push { get; } = new();
    public Mock<IEmailNotificationService> Email { get; } = new();
    public Mock<IImageStorageService> Storage { get; } = new();
    public Mock<IEventVideoStorageService> VideoStorage { get; } = new();
    public Mock<IGalleryMediaStorageService> GalleryStorage { get; } = new();
    public Mock<IDocumentStorageService> Documents { get; } = new();

    public EventsApiFactory()
    {
        Storage.Setup(s => s.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), "events", It.IsAny<string>()))
            .ReturnsAsync("https://pub-test.r2.dev/images/test/events/new.webp");
        GalleryStorage.Setup(s => s.GetMaxFileSize(RTUB.Core.Enums.MediaType.Image)).Returns(10 * 1024 * 1024);
        GalleryStorage.Setup(s => s.GetMaxFileSize(RTUB.Core.Enums.MediaType.Video)).Returns(100 * 1024 * 1024);
        GalleryStorage.Setup(s => s.UploadMediaAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RTUB.Core.Enums.MediaType>(),
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<byte?>(), It.IsAny<byte?>()))
            .ReturnsAsync(() => $"https://pub-test.r2.dev/images/test/gallery/{Guid.NewGuid():N}.jpg");
        Email.Setup(m => m.SendEventReminderNotificationAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<Dictionary<string, (string, string)>>(), It.IsAny<string>(),
                It.IsAny<DateTime?>(), It.IsAny<IProgress<EmailSendProgress>?>()))
            .ReturnsAsync((true, 1, (string?)null));
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
            services.RemoveAll<IImageStorageService>();
            services.AddSingleton(Storage.Object);
            services.RemoveAll<IEventVideoStorageService>();
            services.AddSingleton(VideoStorage.Object);
            services.RemoveAll<IGalleryMediaStorageService>();
            services.AddSingleton(GalleryStorage.Object);
            services.RemoveAll<IDocumentStorageService>();
            services.AddSingleton(Documents.Object);
        });
    }
}
