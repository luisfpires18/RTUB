using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using RTUB.Application.Data;

namespace RTUB.Integration.Tests;

/// <summary>
/// Custom WebApplicationFactory for integration tests that provides:
/// - Isolated in-memory database for each test
/// - Mock credentials from configuration
/// - Prevents role seeding conflicts
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// Connection string for this factory's own database. The name is unique per factory, so
    /// factories (and therefore test classes, which run in parallel) never see each other's data.
    /// <c>Cache=Shared</c> is what lets several connections address the same in-memory database:
    /// every <see cref="ApplicationDbContext"/> then opens its <b>own</b> <see cref="SqliteConnection"/>.
    /// Handing one already-open <see cref="SqliteConnection"/> instance to <c>UseSqlite</c> instead
    /// is what used to corrupt startup — see <see cref="_keepAlive"/>.
    /// </summary>
    private readonly string _connectionString =
        $"Data Source=rtub-tests-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

    /// <summary>
    /// An in-memory SQLite database is dropped as soon as its last connection closes, so one
    /// connection is held open for the lifetime of the factory purely to keep the database alive.
    /// Nothing queries through it: it is opened here, once, and closed in <see cref="Dispose(bool)"/>.
    /// It is deliberately not the connection the DbContexts use — EF Core's SQLite provider
    /// registers its custom functions, collations and aggregates on the connection object each
    /// time a context is constructed, and those registrations write to plain dictionaries on
    /// <see cref="SqliteConnection"/>. Two contexts being constructed at once on one shared
    /// connection therefore corrupted those dictionaries.
    /// </summary>
    private readonly SqliteConnection _keepAlive;

    /// <summary>
    /// This factory's connection string, so a derived factory can re-register
    /// <see cref="ApplicationDbContext"/> against the same database — for example to attach an
    /// interceptor — instead of pointing at a different one.
    /// </summary>
    protected string ConnectionString => _connectionString;

    /// <summary>
    /// Password of the admin that <see cref="SeedData"/> creates for this factory. Generated per
    /// factory instead of committed, and exposed so a test can sign that admin in — the create
    /// and the sign-in must use the same value, so it is held here rather than written twice.
    /// </summary>
    public string AdminPassword { get; } = TestSecret.NewPassword();

    public TestWebApplicationFactory()
    {
        _keepAlive = new SqliteConnection(_connectionString);
        _keepAlive.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Set environment to Test to skip production behaviors
            context.HostingEnvironment.EnvironmentName = "Test";

            // Add test configuration with mock credentials
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AdminUser:Username"] = "testadmin",
                ["AdminUser:Email"] = "testadmin@test.com",
                ["AdminUser:Password"] = AdminPassword,
                ["EmailSettings:SmtpServer"] = "smtp.test.com",
                ["EmailSettings:SmtpPort"] = "587",
                ["EmailSettings:SmtpUsername"] = "test@test.com",
                // Generated, not committed: nothing sends mail under the test host, but a
                // credential-shaped literal keyed "SmtpPassword" is exactly what a scanner flags.
                ["EmailSettings:SmtpPassword"] = TestSecret.NewPassword(),
                ["EmailSettings:SenderEmail"] = "noreply@test.com",
                ["EmailSettings:SenderName"] = "Test RTUB",
                ["IDrive:Endpoint"] = "s3.test.com",
                ["IDrive:Bucket"] = "test-bucket",
                ["IDrive:AccessKey"] = "test-access-key",
                ["IDrive:SecretKey"] = "test-secret-key",
                ["Cloudflare:R2:AccessKeyId"] = "test-cloudflare-access-key",
                ["Cloudflare:R2:SecretAccessKey"] = "test-cloudflare-secret-key",
                ["Cloudflare:R2:AccountId"] = "test-account-id",
                ["Cloudflare:R2:Bucket"] = "test-bucket",
                ["Cloudflare:R2:PublicUrl"] = "https://pub-test.r2.dev"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Lets a test represent a distinct client IP; no-op unless the request opts in.
            services.AddTransient<IStartupFilter, RemoteIpTestStartupFilter>();

            // Remove the existing ApplicationDbContext registration
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();

            // Configured from the connection string, not from a connection instance, so every
            // context opens and owns its own connection.
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlite(_connectionString);
                options.EnableSensitiveDataLogging();
            });

            // Creates the schema and seeds it. Registered as a hosted service rather than done
            // after the host is built, because WebApplicationFactory's host is deferred: touching
            // host.Services is what starts it, so there is no window between "built" and
            // "started" in which to seed.
            services.AddHostedService<DatabaseInitializer>();
        });

        builder.UseEnvironment("Test");
    }

    protected override void Dispose(bool disposing)
    {
        // The host goes first: closing the last connection drops the in-memory database, so the
        // keep-alive connection must outlive anything that might still be querying through it.
        base.Dispose(disposing);

        if (disposing)
        {
            _keepAlive.Dispose();
        }
    }

    /// <summary>
    /// Creates the schema and runs <see cref="SeedData"/>. <see cref="IHostedLifecycleService"/> is
    /// deliberate: every <c>StartingAsync</c> runs before <b>any</b> hosted service's
    /// <c>StartAsync</c>, so the database is complete before the application's background services
    /// — several of which query it immediately — are started. Registration order does not matter.
    /// </summary>
    private sealed class DatabaseInitializer : IHostedLifecycleService
    {
        private readonly IServiceProvider _serviceProvider;

        public DatabaseInitializer(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task StartingAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var services = scope.ServiceProvider;

            await services.GetRequiredService<ApplicationDbContext>().Database
                .EnsureCreatedAsync(cancellationToken);

            var configuration = services.GetRequiredService<IConfiguration>();
            await SeedData.InitializeAsync(services, configuration);
        }

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
