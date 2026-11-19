using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using RTUB.Application.Services.Email;

namespace RTUB.Application.Tests.Services.Email;

/// <summary>
/// Unit tests for EmailRateLimiter
/// Tests rate limiting logic without any network calls
/// </summary>
public class EmailRateLimiterTests : IDisposable
{
    private readonly IMemoryCache _cache;
    private readonly EmailRateLimiter _rateLimiter;

    public EmailRateLimiterTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        _rateLimiter = new EmailRateLimiter(_cache);
    }

    [Fact]
    public void Constructor_WithNullCache_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new EmailRateLimiter(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("cache");
    }

    [Fact]
    public void ShouldRateLimit_FirstCall_ReturnsFalse()
    {
        // Arrange
        var cacheKey = "test-key-1";

        // Act
        var result = _rateLimiter.ShouldRateLimit(cacheKey);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldRateLimit_SecondCallWithSameKey_ReturnsTrue()
    {
        // Arrange
        var cacheKey = "test-key-2";
        _rateLimiter.ShouldRateLimit(cacheKey);

        // Act
        var result = _rateLimiter.ShouldRateLimit(cacheKey);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldRateLimit_DifferentKeys_ReturnsFalseForBoth()
    {
        // Arrange
        var cacheKey1 = "test-key-3a";
        var cacheKey2 = "test-key-3b";

        // Act
        var result1 = _rateLimiter.ShouldRateLimit(cacheKey1);
        var result2 = _rateLimiter.ShouldRateLimit(cacheKey2);

        // Assert
        result1.Should().BeFalse();
        result2.Should().BeFalse();
    }

    [Fact]
    public void ShouldRateLimit_WithNullKey_ThrowsArgumentException()
    {
        // Act
        var act = () => _rateLimiter.ShouldRateLimit(null!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("cacheKey");
    }

    [Fact]
    public void ShouldRateLimit_WithEmptyKey_ThrowsArgumentException()
    {
        // Act
        var act = () => _rateLimiter.ShouldRateLimit(string.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("cacheKey");
    }

    [Fact]
    public void ShouldRateLimit_WithCustomDuration_RespectsDuration()
    {
        // Arrange
        var cacheKey = "test-key-4";
        var duration = TimeSpan.FromMilliseconds(50);

        // Act
        var result1 = _rateLimiter.ShouldRateLimit(cacheKey, duration);
        var result2 = _rateLimiter.ShouldRateLimit(cacheKey, duration);

        // Assert
        result1.Should().BeFalse();
        result2.Should().BeTrue();
    }

    public void Dispose()
    {
        _cache?.Dispose();
    }
}
