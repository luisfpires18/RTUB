using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Core.Tests.Entities;

public class GeocodingCacheTests
{
    [Fact]
    public void Constructor_WithDefaults_ShouldCreateInstance()
    {
        // Act
        var cache = new GeocodingCache();

        // Assert
        cache.Should().NotBeNull();
        cache.CityName.Should().Be("");
        cache.CountryCode.Should().Be("PT");
        cache.Latitude.Should().Be(0);
        cache.Longitude.Should().Be(0);
        cache.Source.Should().Be("Nominatim");
        cache.LastUpdated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Properties_WhenSet_ShouldReturnCorrectValues()
    {
        // Arrange
        var lastUpdated = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var cache = new GeocodingCache
        {
            Id = 1,
            CityName = "lisbon",
            CountryCode = "PT",
            Latitude = 38.7169,
            Longitude = -9.1399,
            LastUpdated = lastUpdated,
            Source = "GoogleMaps"
        };

        // Assert
        cache.Id.Should().Be(1);
        cache.CityName.Should().Be("lisbon");
        cache.CountryCode.Should().Be("PT");
        cache.Latitude.Should().Be(38.7169);
        cache.Longitude.Should().Be(-9.1399);
        cache.LastUpdated.Should().Be(lastUpdated);
        cache.Source.Should().Be("GoogleMaps");
    }

    [Theory]
    [InlineData("porto")]
    [InlineData("braga")]
    [InlineData("faro")]
    public void CityName_WithDifferentValues_ShouldStore(string cityName)
    {
        // Arrange
        var cache = new GeocodingCache { CityName = cityName };

        // Assert
        cache.CityName.Should().Be(cityName);
    }

    [Theory]
    [InlineData("PT")]
    [InlineData("ES")]
    [InlineData("FR")]
    public void CountryCode_WithDifferentValues_ShouldStore(string countryCode)
    {
        // Arrange
        var cache = new GeocodingCache { CountryCode = countryCode };

        // Assert
        cache.CountryCode.Should().Be(countryCode);
    }

    [Fact]
    public void Coordinates_WithValidValues_ShouldStore()
    {
        // Arrange
        var cache = new GeocodingCache
        {
            Latitude = 41.1579,
            Longitude = -8.6291
        };

        // Assert
        cache.Latitude.Should().Be(41.1579);
        cache.Longitude.Should().Be(-8.6291);
    }

    [Fact]
    public void Coordinates_WithNegativeValues_ShouldStore()
    {
        // Arrange
        var cache = new GeocodingCache
        {
            Latitude = -33.8688,
            Longitude = 151.2093
        };

        // Assert
        cache.Latitude.Should().Be(-33.8688);
        cache.Longitude.Should().Be(151.2093);
    }

    [Fact]
    public void BaseEntityProperties_ShouldBeAccessible()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var cache = new GeocodingCache
        {
            CreatedAt = now,
            CreatedBy = "creator",
            UpdatedAt = now.AddHours(1),
            UpdatedBy = "updater"
        };

        // Assert
        cache.CreatedAt.Should().Be(now);
        cache.CreatedBy.Should().Be("creator");
        cache.UpdatedAt.Should().Be(now.AddHours(1));
        cache.UpdatedBy.Should().Be("updater");
    }
}
