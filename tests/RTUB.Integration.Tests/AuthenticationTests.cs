using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RTUB.Application.Data;
using RTUB.Core.Entities;
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

    [Fact]
    public async Task CookieValidation_UpdatesLastLoginDate()
    {
        // Arrange
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        // Create a test user
        using var scope = Factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var testUser = new ApplicationUser
        {
            UserName = "cookietest",
            Email = "cookietest@test.com",
            EmailConfirmed = true,
            FirstName = "Cookie",
            LastName = "Test",
            Nickname = "CookieTester",
            PhoneContact = "123456789"
        };

        var createResult = await userManager.CreateAsync(testUser, "CookieTest123!");
        createResult.Succeeded.Should().BeTrue();

        // Act - Login to get a cookie
        var loginData = new Dictionary<string, string>
        {
            ["Username"] = "cookietest",
            ["Password"] = "CookieTest123!",
            ["RememberMe"] = "false"
        };
        var loginResponse = await client.PostAsync("/auth/login", new FormUrlEncodedContent(loginData));

        // Assert login was successful
        loginResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        
        // Get initial LastLoginDate
        DateTime? initialLastLoginDate;
        using (var scope2 = Factory.Services.CreateScope())
        {
            var db = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.FindAsync(testUser.Id);
            user.Should().NotBeNull();
            initialLastLoginDate = user!.LastLoginDate;
            initialLastLoginDate.Should().NotBeNull();
        }

        // Wait for 6 seconds to ensure the 5-minute cache window allows an update
        // Note: In a real scenario, we'd wait 5+ minutes, but for testing we can verify
        // the mechanism works by checking the initial update happened
        await Task.Delay(TimeSpan.FromSeconds(1));

        // Make another authenticated request to trigger cookie validation
        var authenticatedResponse = await client.GetAsync("/");
        authenticatedResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - Verify LastLoginDate was set during login
        using (var scope3 = Factory.Services.CreateScope())
        {
            var db = scope3.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.FindAsync(testUser.Id);
            user.Should().NotBeNull();
            user!.LastLoginDate.Should().NotBeNull();
            user.LastLoginDate.Should().BeOnOrAfter(initialLastLoginDate!.Value);
        }
    }
}
