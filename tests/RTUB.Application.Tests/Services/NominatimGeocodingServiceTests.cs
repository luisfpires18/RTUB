using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RTUB.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using RTUB.Application.Data;
using Microsoft.EntityFrameworkCore;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for NominatimGeocodingService
/// Note: Most tests use DisabledInTests flag to avoid HTTP calls
/// </summary>
public class NominatimGeocodingServiceTests
{
    private readonly Mock<ILogger<NominatimGeocodingService>> _mockLogger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceProvider _serviceProvider;

    public NominatimGeocodingServiceTests()
    {
        _mockLogger = new Mock<ILogger<NominatimGeocodingService>>();
        
        // Setup HttpClientFactory
        var services = new ServiceCollection();
        services.AddHttpClient("Nominatim")
            .ConfigureHttpClient(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            });
        
        // Add required dependencies for ApplicationDbContext
        services.AddSingleton<Microsoft.AspNetCore.Http.IHttpContextAccessor>(new Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>().Object);
        services.AddScoped<RTUB.Application.Services.AuditContext>();
        
        // Setup in-memory database for testing (scoped)
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase("TestGeocodingDb_" + Guid.NewGuid()));
        
        _serviceProvider = services.BuildServiceProvider();
        _httpClientFactory = _serviceProvider.GetRequiredService<IHttpClientFactory>();
    }

    [Fact]
    public async Task GetCoordinatesAsync_WithDisabledInTests_ReturnsDummyCoordinates()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Geocoding:DisabledInTests", "true" }
            })
            .Build();
        
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = new NominatimGeocodingService(_httpClientFactory, _mockLogger.Object, dbContext, config);
        var cityName = "TestCity";

        // Act
        var result = await service.GetCoordinatesAsync(cityName);

        // Assert
        result.Should().NotBeNull();
        result.Value.Latitude.Should().Be(38.7223);
        result.Value.Longitude.Should().Be(-9.1393);
    }

    [Fact]
    public async Task GetCoordinatesAsync_WithDatabaseCache_ReturnsFromCache()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Geocoding:DisabledInTests", "false" }
            })
            .Build();
        
        // Pre-populate cache
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.GeocodingCaches.Add(new GeocodingCache
        {
            CityName = "lisboa",
            CountryCode = "PT",
            Latitude = 38.7223,
            Longitude = -9.1393,
            LastUpdated = DateTime.UtcNow,
            Source = "Test"
        });
        await dbContext.SaveChangesAsync();
        
        var service = new NominatimGeocodingService(_httpClientFactory, _mockLogger.Object, dbContext, config);

        // Act
        var result = await service.GetCoordinatesAsync("Lisboa", "PT");

        // Assert
        result.Should().NotBeNull();
        result.Value.Latitude.Should().Be(38.7223);
        result.Value.Longitude.Should().Be(-9.1393);
    }

    [Fact]
    public async Task GetCoordinatesAsync_WithEmptyString_ReturnsNull()
    {
        // Arrange
        var config = new ConfigurationBuilder().Build();
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = new NominatimGeocodingService(_httpClientFactory, _mockLogger.Object, dbContext, config);
        var cityName = "";

        // Act
        var result = await service.GetCoordinatesAsync(cityName);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCoordinatesAsync_WithWhitespace_ReturnsNull()
    {
        // Arrange
        var config = new ConfigurationBuilder().Build();
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = new NominatimGeocodingService(_httpClientFactory, _mockLogger.Object, dbContext, config);
        var cityName = "   ";

        // Act
        var result = await service.GetCoordinatesAsync(cityName);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCoordinatesAsync_WithNull_ReturnsNull()
    {
        // Arrange
        var config = new ConfigurationBuilder().Build();
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = new NominatimGeocodingService(_httpClientFactory, _mockLogger.Object, dbContext, config);
        string? cityName = null;

        // Act
        var result = await service.GetCoordinatesAsync(cityName!);

        // Assert
        result.Should().BeNull();
    }
}

