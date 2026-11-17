using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RTUB.Application.Interfaces;
using Xunit;

namespace RTUB.Integration.Tests.Api;

/// <summary>The host with the email sender replaced by a recorder; addresses in <see cref="FailFor"/> throw instead.</summary>
public sealed class AccountRecoveryFactory : TestWebApplicationFactory
{
    public ConcurrentBag<(string To, string Subject, string Html)> Sent { get; } = new();
    public ConcurrentBag<string> FailFor { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
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
