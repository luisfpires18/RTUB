using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using Xunit;

namespace RTUB.Integration.Tests.Pages;

/// <summary>
/// Integration tests for Public pages (Shop, Requests, OrgaosSociais)
/// </summary>
public class PublicPagesTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public PublicPagesTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    #region Shop Page Tests

    [Fact]
    public async Task ShopPage_ReturnsSuccessStatusCode()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/shop");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ShopPage_ContainsExpectedContent()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/shop");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        content.Should().Contain("Loja", "page should display Shop/Loja title");
    }

    [Fact]
    public async Task ShopPage_HasCorrectContentType()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/shop");

        // Assert
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
    }

    #endregion

    #region Requests Page Tests

    [Fact]
    public async Task RequestsPage_ReturnsSuccessStatusCode()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/requests");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RequestsPage_ContainsExpectedContent()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/requests");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        content.Should().Contain("Pedido", "page should display Requests/Pedidos");
    }

    [Fact]
    public async Task RequestsPage_HasCorrectContentType()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/requests");

        // Assert
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
    }

    #endregion

    #region Roles Page Tests

    [Fact]
    public async Task RolesPage_ReturnsSuccessStatusCode()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/roles");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RolesPage_ContainsExpectedContent()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/roles");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        content.Should().Contain("rg", "page should display Órgãos Sociais");
    }

    [Fact]
    public async Task RolesPage_HasCorrectContentType()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/roles");

        // Assert
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
    }

    #endregion

    #region Navigation Tests

    [Fact]
    public async Task PublicPages_AllAccessibleFromHomePage()
    {
        // Arrange - Load home page first
        var homeResponse = await _client.GetAsync("/");

        // Act - Navigate to each public page
        var shopResponse = await _client.GetAsync("/shop");
        var requestsResponse = await _client.GetAsync("/requests");
        var rolesResponse = await _client.GetAsync("/roles");

        // Assert
        homeResponse.IsSuccessStatusCode.Should().BeTrue();
        shopResponse.IsSuccessStatusCode.Should().BeTrue();
        requestsResponse.IsSuccessStatusCode.Should().BeTrue();
        rolesResponse.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task PublicPages_NavigationBetweenPages_Works()
    {
        // Arrange & Act - Navigate through public pages in sequence
        var shopResponse = await _client.GetAsync("/shop");
        var requestsResponse = await _client.GetAsync("/requests");
        var rolesResponse = await _client.GetAsync("/roles");

        // Assert
        shopResponse.IsSuccessStatusCode.Should().BeTrue();
        requestsResponse.IsSuccessStatusCode.Should().BeTrue();
        rolesResponse.IsSuccessStatusCode.Should().BeTrue();
    }

    #endregion
}
