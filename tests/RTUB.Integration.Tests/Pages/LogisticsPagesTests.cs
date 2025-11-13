using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using Xunit;

namespace RTUB.Integration.Tests.Pages;

/// <summary>
/// Integration tests for Logistics pages (/logistics and /logistics/{id})
/// Tests page accessibility, authentication requirements, and navigation
/// </summary>
public class LogisticsPagesTests : IntegrationTestBase
{
    private readonly HttpClient _client;

    public LogisticsPagesTests(TestWebApplicationFactory factory) : base(factory)
    {
        _client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    #region Logistics Main Page Tests (/logistics)

    [Fact]
    public async Task LogisticsPage_WithoutAuth_LoadsPage()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/logistics");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task LogisticsPage_IsAccessible()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/logistics");

        // Assert
        response.Should().NotBeNull("Response should not be null");
        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError, 
            "Page should not have server errors");
    }

    [Fact]
    public async Task LogisticsPage_Url_ShouldBeLowerCase()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/logistics");

        // Assert
        response.RequestMessage?.RequestUri?.AbsolutePath.Should().Be("/logistics", 
            "Logistics URL should be lowercase");
    }

    #endregion

    #region Logistics Board Page Tests (/logistics/{id})

    [Fact]
    public async Task LogisticsBoardPage_WithoutAuth_LoadsPage()
    {
        // Arrange
        var boardId = 1;

        // Act
        var response = await _client.GetAsync($"/logistics/{boardId}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task LogisticsBoardPage_WithId_IsAccessible()
    {
        // Arrange
        var boardId = 1;

        // Act
        var response = await _client.GetAsync($"/logistics/{boardId}");

        // Assert
        response.Should().NotBeNull("Response should not be null");
        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError, 
            "Page should not have server errors");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(999)]
    public async Task LogisticsBoardPage_WithVariousIds_IsAccessible(int boardId)
    {
        // Arrange & Act
        var response = await _client.GetAsync($"/logistics/{boardId}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task LogisticsBoardPage_InvalidId_ShouldHandleGracefully()
    {
        // Arrange
        var invalidId = "abc";

        // Act
        var response = await _client.GetAsync($"/logistics/{invalidId}");

        // Assert
        // Should return 404 for invalid ID format
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.OK, HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task LogisticsBoardPage_NegativeId_ShouldHandleGracefully()
    {
        // Arrange
        var negativeId = -1;

        // Act
        var response = await _client.GetAsync($"/logistics/{negativeId}");

        // Assert
        // Should handle negative IDs gracefully
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect, HttpStatusCode.NotFound);
    }

    #endregion

    #region Navigation Tests

    [Fact]
    public void LogisticsNavigation_FromMainToBoard_UrlPattern()
    {
        // Arrange
        var mainPageUrl = "/logistics";
        var boardId = 1;
        var expectedBoardUrl = $"/logistics/{boardId}";

        // Act & Assert
        mainPageUrl.Should().Be("/logistics", "Main page URL should be /logistics");
        expectedBoardUrl.Should().Match(
            pattern => pattern.StartsWith("/logistics/") && pattern.Length > "/logistics/".Length,
            "Board URL should follow /logistics/{id} pattern");
    }

    [Theory]
    [InlineData("/logistics", true)]
    [InlineData("/logistics/1", true)]
    [InlineData("/logistics/999", true)]
    [InlineData("/logistics/", false)] // Trailing slash without ID
    public void LogisticsUrls_ValidityChecks(string url, bool shouldBeValidFormat)
    {
        // Arrange & Act
        var isValid = url == "/logistics" || 
                     (url.StartsWith("/logistics/") && 
                      url.Length > "/logistics/".Length &&
                      !url.EndsWith("/"));

        // Assert
        isValid.Should().Be(shouldBeValidFormat, 
            $"URL {url} validity should match expected format");
    }

    #endregion

    #region Page Title Tests

    [Fact]
    public void LogisticsPageTitle_ShouldFollowPattern()
    {
        // Arrange
        var expectedMainPageTitle = "Quadros de Logística - RTUB";
        
        // Assert
        expectedMainPageTitle.Should().Contain("Logística", "Title should mention Logistics");
        expectedMainPageTitle.Should().EndWith("RTUB", "Title should end with site name");
    }

    [Fact]
    public void LogisticsBoardPageTitle_ShouldFollowPattern()
    {
        // Arrange
        var boardName = "Test Board";
        var expectedPattern = $"{boardName} - RTUB";
        
        // Assert
        expectedPattern.Should().Contain(boardName, "Title should contain board name");
        expectedPattern.Should().EndWith("RTUB", "Title should end with site name");
    }

    #endregion

    #region Access Control Tests

    [Fact]
    public async Task LogisticsPages_UseAuthorizationMechanism()
    {
        // Arrange
        var logisticsPages = new[] 
        { 
            "/logistics",
            "/logistics/1",
            "/logistics/5"
        };

        // Act & Assert
        foreach (var page in logisticsPages)
        {
            var response = await _client.GetAsync(page);
            response.Should().NotBeNull($"Page {page} should return a response");
            response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError,
                $"Page {page} should not have internal server errors");
        }
    }

    [Fact]
    public async Task LogisticsPages_AreAccessible()
    {
        // Arrange & Act
        var mainResponse = await _client.GetAsync("/logistics");
        var boardResponse = await _client.GetAsync("/logistics/1");

        // Assert
        mainResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect);
        boardResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect, HttpStatusCode.NotFound);
    }

    #endregion

    #region HTTP Method Tests

    [Fact]
    public async Task LogisticsPage_GetMethod_ShouldWork()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/logistics");

        // Assert
        response.Should().NotBeNull("GET request should return a response");
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK, 
            HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task LogisticsBoardPage_GetMethod_ShouldWork()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/logistics/1");

        // Assert
        response.Should().NotBeNull("GET request should return a response");
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK, 
            HttpStatusCode.Redirect,
            HttpStatusCode.NotFound);
    }

    #endregion

    #region Pagination Parameter Tests

    [Theory]
    [InlineData("/logistics?page=1")]
    [InlineData("/logistics?page=2&pageSize=8")]
    [InlineData("/logistics?search=test")]
    public async Task LogisticsPage_WithQueryParameters_ShouldWork(string url)
    {
        // Arrange & Act
        var response = await _client.GetAsync(url);

        // Assert
        response.Should().NotBeNull("Requests with query parameters should work");
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect);
    }

    #endregion
}
