using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for CachedGeocodingService
/// </summary>
public class CachedGeocodingServiceTests
{
    private readonly Mock<ILogger<CachedGeocodingService>> _mockLogger;
    private readonly Mock<IGeocodingQueue> _mockQueue;
    private readonly IServiceProvider _serviceProvider;

    public CachedGeocodingServiceTests()
    {
        _mockLogger = new Mock<ILogger<CachedGeocodingService>>();
        _mockQueue = new Mock<IGeocodingQueue>();
        
        // Setup required dependencies for ApplicationDbContext
        var services = new ServiceCollection();
        services.AddSingleton<Microsoft.AspNetCore.Http.IHttpContextAccessor>(new Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>().Object);
        services.AddScoped<AuditContext>();
        
        // Setup in-memory database for testing (scoped)
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase("TestCachedGeocodingDb_" + Guid.NewGuid()));
        
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task GetCoordinatesAsync_WithCachedCity_ReturnsFromCache()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        // Pre-populate cache
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
        
        var service = new CachedGeocodingService(dbContext, _mockQueue.Object, _mockLogger.Object);

        // Act
        var result = await service.GetCoordinatesAsync("Lisboa", "PT");

        // Assert
        result.Should().NotBeNull();
        result.Value.Latitude.Should().Be(38.7223);
        result.Value.Longitude.Should().Be(-9.1393);
        
        // Verify queue was NOT called
        _mockQueue.Verify(q => q.EnqueueCityAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetCoordinatesAsync_WithUncachedCity_EnqueuesAndReturnsNull()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = new CachedGeocodingService(dbContext, _mockQueue.Object, _mockLogger.Object);

        // Act
        var result = await service.GetCoordinatesAsync("Porto", "PT");

        // Assert
        result.Should().BeNull();
        
        // Verify city was enqueued
        _mockQueue.Verify(q => q.EnqueueCityAsync("porto", "PT"), Times.Once);
    }

    [Fact]
    public async Task GetCoordinatesAsync_WithEmptyString_ReturnsNull()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = new CachedGeocodingService(dbContext, _mockQueue.Object, _mockLogger.Object);

        // Act
        var result = await service.GetCoordinatesAsync("", "PT");

        // Assert
        result.Should().BeNull();
        
        // Verify queue was NOT called
        _mockQueue.Verify(q => q.EnqueueCityAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetCoordinatesAsync_WithWhitespace_ReturnsNull()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = new CachedGeocodingService(dbContext, _mockQueue.Object, _mockLogger.Object);

        // Act
        var result = await service.GetCoordinatesAsync("   ", "PT");

        // Assert
        result.Should().BeNull();
        
        // Verify queue was NOT called
        _mockQueue.Verify(q => q.EnqueueCityAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetCoordinatesAsync_NormalizesCityName_ToLowercase()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        // Add cache with lowercase name
        dbContext.GeocodingCaches.Add(new GeocodingCache
        {
            CityName = "porto",
            CountryCode = "PT",
            Latitude = 41.1579,
            Longitude = -8.6291,
            LastUpdated = DateTime.UtcNow,
            Source = "Test"
        });
        await dbContext.SaveChangesAsync();
        
        var service = new CachedGeocodingService(dbContext, _mockQueue.Object, _mockLogger.Object);

        // Act - Query with uppercase
        var result = await service.GetCoordinatesAsync("PORTO", "PT");

        // Assert
        result.Should().NotBeNull();
        result.Value.Latitude.Should().Be(41.1579);
    }

    [Fact]
    public async Task GetCoordinatesAsync_NormalizesCountryCode_ToUppercase()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        // Add cache with uppercase country code
        dbContext.GeocodingCaches.Add(new GeocodingCache
        {
            CityName = "madrid",
            CountryCode = "ES",
            Latitude = 40.4168,
            Longitude = -3.7038,
            LastUpdated = DateTime.UtcNow,
            Source = "Test"
        });
        await dbContext.SaveChangesAsync();
        
        var service = new CachedGeocodingService(dbContext, _mockQueue.Object, _mockLogger.Object);

        // Act - Query with lowercase country code
        var result = await service.GetCoordinatesAsync("Madrid", "es");

        // Assert
        result.Should().NotBeNull();
        result.Value.Latitude.Should().Be(40.4168);
    }

    [Fact]
    public async Task GetCoordinatesAsync_WithDefaultCountryCode_UsesPT()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = new CachedGeocodingService(dbContext, _mockQueue.Object, _mockLogger.Object);

        // Act
        var result = await service.GetCoordinatesAsync("Braga");

        // Assert
        result.Should().BeNull();
        
        // Verify city was enqueued with PT as country code
        _mockQueue.Verify(q => q.EnqueueCityAsync("braga", "PT"), Times.Once);
    }

    [Fact]
    public async Task GetCoordinatesAsync_WithDifferentCountryCodes_TreatsAsDifferentCities()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        // Add cache for Braga, PT
        dbContext.GeocodingCaches.Add(new GeocodingCache
        {
            CityName = "braga",
            CountryCode = "PT",
            Latitude = 41.5454,
            Longitude = -8.4265,
            LastUpdated = DateTime.UtcNow,
            Source = "Test"
        });
        await dbContext.SaveChangesAsync();
        
        var service = new CachedGeocodingService(dbContext, _mockQueue.Object, _mockLogger.Object);

        // Act - Query with same city name but different country
        var resultPT = await service.GetCoordinatesAsync("Braga", "PT");
        var resultES = await service.GetCoordinatesAsync("Braga", "ES");

        // Assert
        resultPT.Should().NotBeNull();
        resultES.Should().BeNull(); // Not in cache for ES
        
        // Verify Braga, ES was enqueued
        _mockQueue.Verify(q => q.EnqueueCityAsync("braga", "ES"), Times.Once);
    }
}
