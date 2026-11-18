using System.Collections.Concurrent;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RTUB.Application.Interfaces;

namespace RTUB.Integration.Tests.Api;

/// <summary>The host with the email sender replaced by a recorder; addresses in <see cref="FailFor"/> throw instead.</summary>
public sealed class AccountRecoveryFactory : TestWebApplicationFactory
{
    public ConcurrentBag<(string To, string Subject, string Html)> Sent { get; } = new();
    public ConcurrentBag<string> FailFor { get; } = new();

    /// <summary>The configured <c>App:BaseUrl</c>: the address the email links must carry.</summary>
    public const string BaseUrl = "https://www.rtub.test";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["App:BaseUrl"] = BaseUrl }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(new RecordingSender(this));
        });
    }

    private sealed class RecordingSender(AccountRecoveryFactory factory) : IEmailSender
    {
        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            if (factory.FailFor.Contains(email))
            {
                throw new InvalidOperationException("SMTP down");
            }

            factory.Sent.Add((email, subject, htmlMessage));
            return Task.CompletedTask;
        }
    }
}
