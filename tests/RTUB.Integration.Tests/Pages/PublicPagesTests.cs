using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace RTUB.Integration.Tests.Pages;

/// <summary>
/// Integration tests for Public pages (Requests, OrgaosSociais)
/// </summary>
public class PublicPagesTests : IntegrationTestBase
{

    private readonly HttpClient _client;

    public PublicPagesTests(TestWebApplicationFactory factory) : base(factory)
    {
        _client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    #region Roles Page Tests

    // /roles is the React shell since track 008; its content is pinned by GovernanceTests.

    [Fact]
    public async Task RolesPage_ReturnsSuccessStatusCode()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/roles");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RolesPage_HasCorrectContentType()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/roles");

        // Assert
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
    }

    [Fact]
    public async Task RolesPage_WithFiscalYearParam_ReturnsSuccess()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/roles?fy=2024-2025");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Navigation Tests

    [Fact]
    public async Task PublicPages_AllAccessibleFromHomePage()
    {
        // Arrange - Load home page first
        var homeResponse = await _client.GetAsync("/");

        // Act - Navigate to each public page
        var requestsResponse = await _client.GetAsync("/request");
        var rolesResponse = await _client.GetAsync("/roles");

        // Assert
        homeResponse.IsSuccessStatusCode.Should().BeTrue();
        requestsResponse.IsSuccessStatusCode.Should().BeTrue();
        rolesResponse.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task PublicPages_NavigationBetweenPages_Works()
    {
        // Arrange & Act - Navigate through public pages in sequence
        var requestsResponse = await _client.GetAsync("/request");
        var rolesResponse = await _client.GetAsync("/roles");

        // Assert
        requestsResponse.IsSuccessStatusCode.Should().BeTrue();
        rolesResponse.IsSuccessStatusCode.Should().BeTrue();
    }

    #endregion
}
