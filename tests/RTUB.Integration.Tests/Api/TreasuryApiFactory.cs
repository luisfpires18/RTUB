// TreasuryApiFactory: test class moved here from TreasuryApiTests.cs.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Enums;
using Xunit;

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
