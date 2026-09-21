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
    private SqliteConnection? _connection;

    /// <summary>
    /// Password of the admin that <see cref="SeedData"/> creates for this factory. Generated per
    /// factory instead of committed, and exposed so a test can sign that admin in — the create
    /// and the sign-in must use the same value, so it is held here rather than written twice.
    /// </summary>
    public string AdminPassword { get; } = TestSecret.NewPassword();

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

            // Create a shared in-memory SQLite connection
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            // Add DbContext with the shared connection
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlite(_connection);
                options.EnableSensitiveDataLogging();
            });
        });

        builder.UseEnvironment("Test");
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // Initialize the database after the host is built
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        db.Database.EnsureCreated();

        // Seed basic test data
        var configuration = services.GetRequiredService<IConfiguration>();
        SeedData.InitializeAsync(services, configuration).GetAwaiter().GetResult();

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connection?.Close();
            _connection?.Dispose();
        }
        base.Dispose(disposing);
    }
}
