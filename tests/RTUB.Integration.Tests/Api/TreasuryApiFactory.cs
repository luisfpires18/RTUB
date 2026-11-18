// TreasuryApiFactory: test class moved here from TreasuryApiTests.cs.
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using RTUB.Application.Interfaces;

namespace RTUB.Integration.Tests.Api;
/// <summary>The test host for Tesouraria: receipts in memory, push and e-mail recorded, the daily calote reminder off.</summary>
public sealed class TreasuryApiFactory : TestWebApplicationFactory
{
    public FakeReceiptStorage Receipts { get; } = new();
    public Mock<IPushNotificationService> Push { get; } = new();
    public Mock<IEmailNotificationService> Email { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["CalotesNotifications:Enabled"] = "false" }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IReceiptStorageService>();
            services.AddSingleton<IReceiptStorageService>(Receipts);
            services.RemoveAll<IPushNotificationService>();
            services.AddSingleton(Push.Object);
            services.RemoveAll<IEmailNotificationService>();
            services.AddSingleton(Email.Object);
        });
    }
}
