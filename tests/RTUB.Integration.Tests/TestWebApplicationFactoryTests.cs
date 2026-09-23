using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RTUB.Application.Data;

namespace RTUB.Integration.Tests;

/// <summary>
/// Guards the test host's own database lifecycle. These are not tests of the application: they
/// pin the two properties the integration suite needs from <see cref="TestWebApplicationFactory"/>
/// — no DbContext shares a connection object with another, and the database is seeded before the
/// hosted services that read it are started.
/// </summary>
public class TestWebApplicationFactoryTests
{
    [Fact]
    public void EveryDbContextOpensItsOwnConnection()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        using var scopeA = factory.Services.CreateScope();
        using var scopeB = factory.Services.CreateScope();

        var fromScopeA = scopeA.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var fromScopeB = scopeB.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        using var fromDbContextFactory = scopeA.ServiceProvider
            .GetRequiredService<IDbContextFactory<ApplicationDbContext>>()
            .CreateDbContext();

        var connectionA = fromScopeA.Database.GetDbConnection();
        var connectionB = fromScopeB.Database.GetDbConnection();
        var connectionC = fromDbContextFactory.Database.GetDbConnection();

        connectionA.Should().NotBeSameAs(connectionB);
        connectionA.Should().NotBeSameAs(connectionC);
        connectionB.Should().NotBeSameAs(connectionC);

        // Separate connections, one database: all three must still see the seeded data.
        fromScopeA.Users.Any().Should().BeTrue();
        fromScopeB.Users.Any().Should().BeTrue();
        fromDbContextFactory.Users.Any().Should().BeTrue();
    }

    [Fact]
    public void SeedDataIsInPlaceBeforeHostedServicesStart()
    {
        using var factory = new SeedOrderProbeFactory();
        using var client = factory.CreateClient();

        factory.SeededAdminWasVisibleAtHostStart.Should().BeTrue(
            "hosted services query the database as soon as they start, so the seed must already be committed");
    }

    [Fact]
    public async Task ConcurrentDbContextCreationDoesNotCorruptTheDatabase()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        var counts = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(async () =>
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await db.Users.AsNoTracking().CountAsync(TestContext.Current.CancellationToken);
        })));

        counts.Should().OnlyContain(count => count > 0);
    }

    /// <summary>
    /// Adds one extra hosted service that records, at host start, whether the seeded admin is
    /// already in the database.
    /// </summary>
    private sealed class SeedOrderProbeFactory : TestWebApplicationFactory
    {
        public bool SeededAdminWasVisibleAtHostStart { get; private set; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureServices(services =>
                services.AddHostedService(serviceProvider => new SeedOrderProbe(serviceProvider, this)));
        }

        private sealed class SeedOrderProbe : IHostedService
        {
            private readonly IServiceProvider _serviceProvider;
            private readonly SeedOrderProbeFactory _owner;

            public SeedOrderProbe(IServiceProvider serviceProvider, SeedOrderProbeFactory owner)
            {
                _serviceProvider = serviceProvider;
                _owner = owner;
            }

            public Task StartAsync(CancellationToken cancellationToken)
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                _owner.SeededAdminWasVisibleAtHostStart = db.Users.Any(u => u.UserName == "testadmin");
                return Task.CompletedTask;
            }

            public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        }
    }
}
