using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for BackgroundGeocodingWorker
/// Note: These are basic constructor and setup tests.
/// The actual background processing is better tested in integration tests.
/// </summary>
public class BackgroundGeocodingWorkerTests
{
    private readonly Mock<ILogger<BackgroundGeocodingWorker>> _mockLogger;
    private readonly IServiceProvider _serviceProvider;

    public BackgroundGeocodingWorkerTests()
    {
        _mockLogger = new Mock<ILogger<BackgroundGeocodingWorker>>();
        
        // Setup required services
        var services = new ServiceCollection();
        
        // Add HttpClientFactory for NominatimGeocodingService
        services.AddHttpClient("Nominatim")
            .ConfigureHttpClient(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(10);
            });
        
        // Add required dependencies for ApplicationDbContext
        services.AddSingleton<Microsoft.AspNetCore.Http.IHttpContextAccessor>(new Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>().Object);
        services.AddScoped<AuditContext>();
        
        // Setup in-memory database for testing
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase("TestBackgroundWorkerDb_" + Guid.NewGuid()));
        
        // Configuration with DisabledInTests flag
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Geocoding:DisabledInTests", "true" }
            })
            .Build();
        services.AddSingleton<IConfiguration>(config);
        
        // Register NominatimGeocodingService
        services.AddScoped<NominatimGeocodingService>();
        
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public void BackgroundGeocodingWorker_CanBeConstructed()
    {
        // Arrange
        var mockQueue = new Mock<IGeocodingQueue>();
        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();

        // Act
        var worker = new BackgroundGeocodingWorker(_mockLogger.Object, scopeFactory, mockQueue.Object);

        // Assert
        worker.Should().NotBeNull();
    }

    [Fact]
    public async Task BackgroundGeocodingWorker_CanStartAndStop()
    {
        // Arrange
        var mockQueue = new Mock<IGeocodingQueue>();
        mockQueue.Setup(q => q.GetPendingCitiesAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<(string CityName, string CountryCode)>());
        
        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var worker = new BackgroundGeocodingWorker(_mockLogger.Object, scopeFactory, mockQueue.Object);

        // Act
        var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);
        
        // Give it a moment to start
        await Task.Delay(100);
        
        // Stop the worker
        await worker.StopAsync(CancellationToken.None);

        // Assert - Worker should have logged startup
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("started")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}
