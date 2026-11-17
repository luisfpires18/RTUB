using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using RTUB.Application.Interfaces;

namespace RTUB.Integration.Tests.Api;

/// <summary>The host with media storage and push replaced by recording fakes.</summary>
public sealed class NaipesApiFactory : TestWebApplicationFactory
{
    public Mock<IPushNotificationService> Push { get; } = new();
    public Mock<INaipeMediaStorageService> Media { get; } = new();

    public NaipesApiFactory()
    {
        Media.Setup(m => m.UploadVideoAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((Stream _, string name, string _, string folder) => $"https://pub-test.r2.dev/naipes/test/videos/{folder}/{Guid.NewGuid():N}_{name}");
        Media.Setup(m => m.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((Stream _, string name, string _, string folder) => $"https://pub-test.r2.dev/naipes/test/images/{folder}/{Guid.NewGuid():N}_{name}");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPushNotificationService>();
            services.AddSingleton(Push.Object);
            services.RemoveAll<INaipeMediaStorageService>();
            services.AddSingleton(Media.Object);
        });
    }
}
