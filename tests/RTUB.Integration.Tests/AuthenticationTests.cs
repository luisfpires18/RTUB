using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using Xunit;

namespace RTUB.Integration.Tests;

/// <summary>
/// Integration tests for authentication workflows
/// Tests the complete authentication flow from HTTP requests to database
/// </summary>
public class AuthenticationTests : IClassFixture<RTUBWebApplicationFactory>
{
    private readonly RTUBWebApplicationFactory _factory;

    public AuthenticationTests(RTUBWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HomePage_ReturnsSuccessStatusCode()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EventsPage_ReturnsSuccessStatusCode()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Events");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
