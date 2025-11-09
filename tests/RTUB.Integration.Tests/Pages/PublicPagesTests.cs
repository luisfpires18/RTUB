using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using Xunit;

namespace RTUB.Integration.Tests.Pages;

/// <summary>
/// Integration tests for Public pages (Requests, OrgaosSociais)
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

    #region Requests Page Tests

    [Fact]
    public async Task RequestsPage_ReturnsSuccessStatusCode()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/request");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RequestsPage_ContainsExpectedContent()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/request");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        content.Should().Contain("Pedido", "page should display Requests/Pedidos");
    }

    [Fact]
    public async Task RequestsPage_HasCorrectContentType()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/request");

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

    [Fact]
    public async Task RolesPage_WithFiscalYearParam_ReturnsSuccess()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/roles?fy=2024-2025");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RolesPage_WithManageParam_ReturnsSuccess()
    {
        // Arrange & Act - manage=1 parameter (would open fiscal year modal for admins)
        var response = await _client.GetAsync("/roles?manage=1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RolesPage_HasFiscalYearDropdown()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/roles");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        content.Should().Contain("select", "page should have fiscal year dropdown select element");
    }

    [Fact]
    public async Task RolesPage_ContainsPositionSections()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/roles");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        content.Should().Contain("DIREÇÃO", "page should have Direção section");
        content.Should().Contain("MESA DE ASSEMBLEIA", "page should have Mesa section");
        content.Should().Contain("CONSELHO FISCAL", "page should have Conselho Fiscal section");
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
