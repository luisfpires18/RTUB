using FluentAssertions;
using RTUB.Application.Services;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for InMemoryGeocodingQueue
/// </summary>
public class InMemoryGeocodingQueueTests
{
    [Fact]
    public async Task EnqueueCityAsync_AddsCity_ToQueue()
    {
        // Arrange
        var queue = new InMemoryGeocodingQueue();
        var cityName = "Porto";
        var countryCode = "PT";

        // Act
        await queue.EnqueueCityAsync(cityName, countryCode);
        var cities = await queue.GetPendingCitiesAsync(10);

        // Assert
        cities.Should().HaveCount(1);
        cities[0].CityName.Should().Be("porto"); // Normalized to lowercase
        cities[0].CountryCode.Should().Be("PT"); // Uppercased
    }

    [Fact]
    public async Task EnqueueCityAsync_WithDuplicates_OnlyAddsOnce()
    {
        // Arrange
        var queue = new InMemoryGeocodingQueue();
        var cityName = "Lisboa";
        var countryCode = "PT";

        // Act
        await queue.EnqueueCityAsync(cityName, countryCode);
        await queue.EnqueueCityAsync(cityName, countryCode);
        await queue.EnqueueCityAsync("LISBOA", countryCode); // Different case
        var cities = await queue.GetPendingCitiesAsync(10);

        // Assert
        cities.Should().HaveCount(1);
    }

    [Fact]
    public async Task EnqueueCityAsync_WithEmptyString_DoesNotAddToQueue()
    {
        // Arrange
        var queue = new InMemoryGeocodingQueue();

        // Act
        await queue.EnqueueCityAsync("", "PT");
        await queue.EnqueueCityAsync("   ", "PT");
        var cities = await queue.GetPendingCitiesAsync(10);

        // Assert
        cities.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPendingCitiesAsync_RespectsMaxCount()
    {
        // Arrange
        var queue = new InMemoryGeocodingQueue();

        // Enqueue 5 cities
        for (int i = 0; i < 5; i++)
        {
            await queue.EnqueueCityAsync($"City{i}", "PT");
        }

        // Act
        var cities = await queue.GetPendingCitiesAsync(3);

        // Assert
        cities.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetPendingCitiesAsync_RemovesCitiesFromQueue()
    {
        // Arrange
        var queue = new InMemoryGeocodingQueue();
        await queue.EnqueueCityAsync("Porto", "PT");
        await queue.EnqueueCityAsync("Lisboa", "PT");

        // Act
        var firstBatch = await queue.GetPendingCitiesAsync(10);
        var secondBatch = await queue.GetPendingCitiesAsync(10);

        // Assert
        firstBatch.Should().HaveCount(2);
        secondBatch.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPendingCitiesAsync_AllowsReenqueuing_AfterDequeue()
    {
        // Arrange
        var queue = new InMemoryGeocodingQueue();
        await queue.EnqueueCityAsync("Porto", "PT");

        // Act
        var firstBatch = await queue.GetPendingCitiesAsync(10);
        await queue.EnqueueCityAsync("Porto", "PT"); // Re-enqueue same city
        var secondBatch = await queue.GetPendingCitiesAsync(10);

        // Assert
        firstBatch.Should().HaveCount(1);
        secondBatch.Should().HaveCount(1);
    }

    [Fact]
    public async Task EnqueueCityAsync_NormalizesCountryCode_ToUpperCase()
    {
        // Arrange
        var queue = new InMemoryGeocodingQueue();

        // Act
        await queue.EnqueueCityAsync("Madrid", "es");
        var cities = await queue.GetPendingCitiesAsync(10);

        // Assert
        cities[0].CountryCode.Should().Be("ES");
    }

    [Fact]
    public async Task GetPendingCitiesAsync_WithEmptyQueue_ReturnsEmptyList()
    {
        // Arrange
        var queue = new InMemoryGeocodingQueue();

        // Act
        var cities = await queue.GetPendingCitiesAsync(10);

        // Assert
        cities.Should().BeEmpty();
    }

    [Fact]
    public async Task EnqueueCityAsync_WithDefaultCountryCode_UsesPortugal()
    {
        // Arrange
        var queue = new InMemoryGeocodingQueue();

        // Act
        await queue.EnqueueCityAsync("Porto");
        var cities = await queue.GetPendingCitiesAsync(10);

        // Assert
        cities[0].CountryCode.Should().Be("PT");
    }
}
