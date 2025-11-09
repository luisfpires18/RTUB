using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace RTUB.Integration.Tests;

/// <summary>
/// Custom WebApplicationFactory for integration tests
/// Configures the test environment with test-specific settings
/// </summary>
public class RTUBWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Clear existing configuration sources
            config.Sources.Clear();
            
            // Add base appsettings.json first
            config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
            
            // Add test-specific configuration
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Use in-memory database for tests
                ["UseSqlServer"] = "false",
                
                // Test email settings (no actual emails sent)
                ["EmailSettings:SmtpServer"] = "smtp.test.com",
                ["EmailSettings:SmtpPort"] = "587",
                ["EmailSettings:SmtpUsername"] = "test@test.com",
                ["EmailSettings:SmtpPassword"] = "test-password",
                ["EmailSettings:SenderEmail"] = "noreply@test.com",
                ["EmailSettings:SenderName"] = "RTUB Test",
                ["EmailSettings:RecipientEmail"] = "admin@test.com",
                ["EmailSettings:EnableSsl"] = "false",
                
                // Test image caching
                ["ImageCaching:CacheDurationInSeconds"] = "1",
                
                // Test IDrive settings (mock values)
                ["IDrive:Endpoint"] = "s3.test.com",
                ["IDrive:Bucket"] = "test-bucket",
                ["IDrive:AccessKey"] = "test-access-key",
                ["IDrive:SecretKey"] = "test-secret-key",
                ["IDrive:WriteAccessKey"] = "test-write-access-key",
                ["IDrive:WriteSecretKey"] = "test-write-secret-key",
                
                // Reduce logging noise in tests
                ["Logging:LogLevel:Default"] = "Warning",
                ["Logging:LogLevel:Microsoft.AspNetCore"] = "Warning",
                ["Logging:LogLevel:Microsoft.EntityFrameworkCore"] = "Warning"
            });
        });

        base.ConfigureWebHost(builder);
    }
}
