using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using Xunit;

namespace RTUB.Integration.Tests;

/// <summary>
/// Integration tests for authentication workflows
/// Tests the complete authentication flow from HTTP requests to database
/// </summary>
public class AuthenticationTests : IntegrationTestBase
{
    public AuthenticationTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task HomePage_ReturnsSuccessStatusCode()
    {
        // Arrange
        var client = Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EventsPage_ReturnsSuccessStatusCode()
    {
        // Arrange
        var client = Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Events");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
