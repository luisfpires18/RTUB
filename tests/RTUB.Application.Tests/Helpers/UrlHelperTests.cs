using FluentAssertions;
using RTUB.Application.Helpers;

namespace RTUB.Application.Tests.Helpers;

/// <summary>
/// Unit tests for UrlHelper
/// Tests URL validation for security (open redirect prevention)
/// </summary>
public class UrlHelperTests
{
    [Fact]
    public void IsLocalUrl_WithNullUrl_ReturnsFalse()
    {
        // Act
        var result = UrlHelper.IsLocalUrl(null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsLocalUrl_WithEmptyUrl_ReturnsFalse()
    {
        // Act
        var result = UrlHelper.IsLocalUrl(string.Empty);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsLocalUrl_WithWhitespaceUrl_ReturnsFalse()
    {
        // Act
        var result = UrlHelper.IsLocalUrl("   ");

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/home")]
    [InlineData("/home/index")]
    [InlineData("/users/profile")]
    [InlineData("/api/data")]
    [InlineData("/page?query=value")]
    [InlineData("/page#anchor")]
    public void IsLocalUrl_WithValidLocalUrl_ReturnsTrue(string url)
    {
        // Act
        var result = UrlHelper.IsLocalUrl(url);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("//evil.com")]
    [InlineData("//evil.com/path")]
    public void IsLocalUrl_WithDoubleSlash_ReturnsFalse(string url)
    {
        // Act
        var result = UrlHelper.IsLocalUrl(url);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("http://evil.com")]
    [InlineData("https://evil.com")]
    [InlineData("ftp://evil.com")]
    public void IsLocalUrl_WithAbsoluteUrl_ReturnsFalse(string url)
    {
        // Act
        var result = UrlHelper.IsLocalUrl(url);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("/\\evil.com")]
    public void IsLocalUrl_WithBackslashEscape_ReturnsFalse(string url)
    {
        // Act
        var result = UrlHelper.IsLocalUrl(url);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("evil.com")]
    [InlineData("www.evil.com")]
    [InlineData("home")]
    [InlineData("page/subpage")]
    public void IsLocalUrl_WithRelativePathNoSlash_ReturnsFalse(string url)
    {
        // Act
        var result = UrlHelper.IsLocalUrl(url);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsLocalUrl_WithSchemeInPath_ReturnsFalse()
    {
        // Arrange
        var url = "/redirect?url=http://evil.com";

        // Act
        var result = UrlHelper.IsLocalUrl(url);

        // Assert - Contains :// which is blocked
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("/Events")]
    [InlineData("/Members/List")]
    [InlineData("/Admin/Dashboard")]
    [InlineData("/Rehearsals/123")]
    public void IsLocalUrl_WithTypicalAppRoutes_ReturnsTrue(string url)
    {
        // Act
        var result = UrlHelper.IsLocalUrl(url);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("/page?returnUrl=/home")]
    [InlineData("/login?next=/dashboard")]
    public void IsLocalUrl_WithQueryParams_ReturnsTrue(string url)
    {
        // Act
        var result = UrlHelper.IsLocalUrl(url);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsLocalUrl_WithFragment_ReturnsTrue()
    {
        // Act
        var result = UrlHelper.IsLocalUrl("/page#section");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsLocalUrl_WithEncodedCharacters_ReturnsTrue()
    {
        // Act
        var result = UrlHelper.IsLocalUrl("/search?q=hello%20world");

        // Assert
        result.Should().BeTrue();
    }
}
